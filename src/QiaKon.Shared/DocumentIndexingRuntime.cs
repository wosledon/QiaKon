using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;
using QiaKon.Llm;
using QiaKon.Retrieval;
using QiaKon.Retrieval.Chunnking;
using QiaKon.Retrieval.Chunnking.MoE;
using QiaKon.Retrieval.DocumentProcessor;
using QiaKon.Retrieval.Embedding;
using QiaKon.Retrieval.VectorStore;

namespace QiaKon.Shared;

internal sealed class DocumentIndexingRuntime
{
    private const string CollectionName = "rag_documents";

    private readonly QiaKonAppDbContext _dbContext;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IDocumentProcessor _documentProcessor;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IMoEChunkingStrategyFactory _moeChunkingStrategyFactory;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly ConfiguredLlmModelResolver _modelResolver;
    private readonly DocumentGraphProjectionService _documentGraphProjectionService;
    private readonly ILogger<DocumentIndexingRuntime>? _logger;
    private readonly CharacterChunkingStrategy _characterChunkingStrategy = new();

    public DocumentIndexingRuntime(
        QiaKonAppDbContext dbContext,
        IHostEnvironment hostEnvironment,
        IDocumentProcessor documentProcessor,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IMoEChunkingStrategyFactory moeChunkingStrategyFactory,
        ILlmClientFactory llmClientFactory,
        ConfiguredLlmModelResolver modelResolver,
        DocumentGraphProjectionService documentGraphProjectionService,
        ILogger<DocumentIndexingRuntime>? logger = null)
    {
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
        _documentProcessor = documentProcessor;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _moeChunkingStrategyFactory = moeChunkingStrategyFactory;
        _llmClientFactory = llmClientFactory;
        _modelResolver = modelResolver;
        _documentGraphProjectionService = documentGraphProjectionService;
        _logger = logger;
    }

    public async Task<IndexedDocumentResult> ProcessAndIndexAsync(
        DocumentRow document,
        string? requestedChunkingStrategy = null,
        CancellationToken cancellationToken = default)
    {
        document.IndexStatus = IndexStatus.Indexing;
        document.IndexProgress = 5;
        document.IndexStartedAt = DateTime.UtcNow;
        document.IndexCompletedAt = null;
        document.IndexErrorMessage = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _documentGraphProjectionService.DeleteDocumentGraphAsync(document.Id, cancellationToken);

        var existingChunkIds = await _dbContext.DocumentChunks
            .Where(c => c.DocumentId == document.Id)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (existingChunkIds.Count > 0)
        {
            var existingChunks = await _dbContext.DocumentChunks.Where(c => c.DocumentId == document.Id).ToListAsync(cancellationToken);
            _dbContext.DocumentChunks.RemoveRange(existingChunks);
            await DeleteVectorRecordsAsync(existingChunkIds, cancellationToken);
        }

        document.IndexProgress = 15;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var extraction = await ExtractContentAsync(document, cancellationToken);
        document.Content = extraction.Content;
        document.Size = extraction.Size;

        var resolvedStrategy = ResolveChunkingStrategy(document, requestedChunkingStrategy);
        UpsertChunkingMetadata(document, resolvedStrategy);

        var chunks = await ChunkAsync(document.Id, extraction.Content, resolvedStrategy, cancellationToken);
        var chunkRows = chunks.Select(chunk => new DocumentChunkRow
        {
            Id = chunk.Id,
            DocumentId = document.Id,
            Content = chunk.Text,
            Order = chunk.Sequence + 1,
            ChunkingStrategy = resolvedStrategy,
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        _dbContext.DocumentChunks.AddRange(chunkRows);
        document.IndexProgress = 45;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await UpsertVectorsAsync(document, chunks, resolvedStrategy, cancellationToken);
        await _documentGraphProjectionService.SyncDocumentAsync(document, chunkRows, cancellationToken);

        document.IndexStatus = IndexStatus.Completed;
        document.IndexVersion = (document.IndexVersion ?? 0) + 1;
        document.IndexProgress = 100;
        document.IndexCompletedAt = DateTime.UtcNow;
        document.IndexErrorMessage = null;
        document.ModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IndexedDocumentResult(document.Id, resolvedStrategy, chunkRows.Count);
    }

    public async Task DeleteDocumentIndexAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _documentGraphProjectionService.DeleteDocumentGraphAsync(documentId, cancellationToken);

        var chunkIds = await _dbContext.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (chunkIds.Count == 0)
        {
            return;
        }

        await DeleteVectorRecordsAsync(chunkIds, cancellationToken);
        var chunkRows = await _dbContext.DocumentChunks.Where(c => c.DocumentId == documentId).ToListAsync(cancellationToken);
        _dbContext.DocumentChunks.RemoveRange(chunkRows);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(DocumentRow document, Exception ex, CancellationToken cancellationToken = default)
    {
        _dbContext.ChangeTracker.Clear();

        var persistedDocument = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == document.Id, cancellationToken);
        if (persistedDocument is null)
        {
            _logger?.LogWarning("标记索引失败状态时未找到文档: {DocumentId}", document.Id);
            return;
        }

        persistedDocument.IndexStatus = IndexStatus.Failed;
        persistedDocument.IndexProgress = 0;
        persistedDocument.IndexCompletedAt = null;
        persistedDocument.IndexErrorMessage = ex.Message;
        persistedDocument.ModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ExtractedContentResult> ExtractContentAsync(DocumentRow document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            return new ExtractedContentResult(document.Content ?? string.Empty, document.Content?.Length ?? 0);
        }

        var fullPath = Path.Combine(_hostEnvironment.ContentRootPath, document.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return new ExtractedContentResult(document.Content ?? string.Empty, document.Content?.Length ?? 0);
        }

        var metadata = PostgresDocumentService.ParseJson(document.MetadataJson);
        var originalFileName = metadata?["originalFileName"]?.GetValue<string>() ?? Path.GetFileName(fullPath);
        var contentType = metadata?["contentType"]?.GetValue<string>();

        if (PostgresDocumentService.IsPlainText(originalFileName, contentType))
        {
            var raw = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return new ExtractedContentResult(raw, raw.Length);
        }

        try
        {
            var processed = await _documentProcessor.ProcessFileAsync(fullPath, cancellationToken);
            return new ExtractedContentResult(processed.Content, processed.Content.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "文档处理器解析失败，回退到现有文本内容。DocumentId={DocumentId}", document.Id);
            return new ExtractedContentResult(document.Content ?? string.Empty, document.Content?.Length ?? 0);
        }
    }

    private string ResolveChunkingStrategy(DocumentRow document, string? requestedChunkingStrategy)
    {
        if (!string.IsNullOrWhiteSpace(requestedChunkingStrategy))
        {
            return requestedChunkingStrategy;
        }

        var metadata = PostgresDocumentService.ParseJson(document.MetadataJson);
        var stored = metadata?["chunkingStrategy"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        return _modelResolver.TryGetDefaultChunkingModel() is not null ? "MoE" : "Character";
    }

    private void UpsertChunkingMetadata(DocumentRow document, string chunkingStrategy)
    {
        var metadata = PostgresDocumentService.ParseJson(document.MetadataJson) ?? new JsonObject();
        metadata["chunkingStrategy"] = chunkingStrategy;
        document.MetadataJson = PostgresDocumentService.SerializeJson(metadata);
    }

    private async Task<IReadOnlyList<QiaKon.Retrieval.IChunk>> ChunkAsync(
        Guid documentId,
        string content,
        string chunkingStrategy,
        CancellationToken cancellationToken)
    {
        if (string.Equals(chunkingStrategy, "moe", StringComparison.OrdinalIgnoreCase))
        {
            var chunkingModel = _modelResolver.TryGetDefaultChunkingModel();
            if (chunkingModel is null)
            {
                _logger?.LogWarning("未找到已启用的默认分块模型，自动回退到 Character 分块。");
                return await _characterChunkingStrategy.ChunkAsync(documentId, content, cancellationToken);
            }

            var options = _modelResolver.BuildOptions(chunkingModel);
            var llmClient = _llmClientFactory.CreateClient(options);
            try
            {
                var strategy = _moeChunkingStrategyFactory.Create(llmClient, new MoEChunkingOptions());
                return await strategy.ChunkAsync(documentId, content, cancellationToken);
            }
            finally
            {
                await llmClient.DisposeAsync();
            }
        }

        return await _characterChunkingStrategy.ChunkAsync(documentId, content, cancellationToken);
    }

    private async Task UpsertVectorsAsync(
        DocumentRow document,
        IReadOnlyList<QiaKon.Retrieval.IChunk> chunks,
        string chunkingStrategy,
        CancellationToken cancellationToken)
    {
        var chunkTexts = chunks.Select(c => c.Text).ToList();
        var embeddings = await _embeddingService.EmbedBatchAsync(chunkTexts, cancellationToken);

        var collection = await _vectorStore.GetOrCreateCollectionAsync(CollectionName, _embeddingService.Dimensions, cancellationToken);
        await collection.EnsureExistsAsync(cancellationToken);

        var records = new List<VectorRecord>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = embeddings[i];
            records.Add(new VectorRecord
            {
                Id = chunk.Id,
                Embedding = embedding,
                Text = chunk.Text,
                Metadata = new Dictionary<string, object?>
                {
                    ["documentId"] = document.Id.ToString(),
                    ["documentTitle"] = document.Title,
                    ["sequence"] = chunk.Sequence,
                    ["startIndex"] = chunk.StartIndex,
                    ["endIndex"] = chunk.EndIndex,
                    ["chunkingStrategy"] = chunkingStrategy,
                    ["documentType"] = document.Type.ToString(),
                }
            });
        }

        await collection.UpsertBatchAsync(records, cancellationToken);
        document.IndexProgress = 90;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteVectorRecordsAsync(IEnumerable<Guid> chunkIds, CancellationToken cancellationToken)
    {
        var ids = chunkIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var collection = await _vectorStore.GetOrCreateCollectionAsync(CollectionName, _embeddingService.Dimensions, cancellationToken);
        await collection.DeleteBatchAsync(ids, cancellationToken);
    }
}

internal sealed record IndexedDocumentResult(Guid DocumentId, string ChunkingStrategy, int ChunkCount);

internal sealed record ExtractedContentResult(string Content, long Size);