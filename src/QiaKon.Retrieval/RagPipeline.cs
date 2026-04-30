using Microsoft.Extensions.Logging;
using QiaKon.Retrieval.Embedding;
using QiaKon.Retrieval.VectorStore;
using System.Text.Json;

namespace QiaKon.Retrieval;

/// <summary>
/// RAG（检索增强生成）管道默认实现
/// </summary>
public sealed class RagPipeline : IRagPipeline
{
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<RagPipeline>? _logger;

    // 内存索引：文档ID -> 文档信息
    private readonly Dictionary<Guid, RagDocumentRecord> _documentIndex = new();

    public RagPipeline(
        IChunkingStrategy chunkingStrategy,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<RagPipeline>? logger = null)
    {
        _chunkingStrategy = chunkingStrategy ?? throw new ArgumentNullException(nameof(chunkingStrategy));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _logger = logger;
    }

    public async Task<RagDocumentRecord> IndexAsync(
        IDocument document,
        CancellationToken cancellationToken = default)
    {
        return await IndexAsync(document, skipProcessing: false, cancellationToken);
    }

    public async Task<RagDocumentRecord> IndexAsync(
        IDocument document,
        bool skipProcessing,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始索引文档: {DocumentId}, 标题: {Title}, 跳过处理: {SkipProcessing}",
            document.Id, document.Title, skipProcessing);

        // 1. 分块
        var chunks = await _chunkingStrategy.ChunkAsync(document.Id, document.Content, cancellationToken);
        _logger?.LogDebug("文档分块完成，共 {Count} 个块", chunks.Count);

        // 2. 生成 Embedding
        var chunkTexts = chunks.Select(c => c.Text).ToList();
        var embeddings = await _embeddingService.EmbedBatchAsync(chunkTexts, cancellationToken);
        _logger?.LogDebug("Embedding 生成完成，共 {Count} 个向量", embeddings.Count);

        // 3. 存储到向量数据库
        var collection = await _vectorStore.GetOrCreateCollectionAsync(
            "rag_documents",
            _embeddingService.Dimensions,
            cancellationToken);

        await collection.EnsureExistsAsync(cancellationToken);

        var chunkIds = new List<Guid>();
        var records = new List<VectorRecord>();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = embeddings[i];

            var record = new VectorRecord
            {
                Id = chunk.Id,
                Embedding = embedding,
                Text = chunk.Text,
                Metadata = new Dictionary<string, object?>
                {
                    ["documentId"] = document.Id,
                    ["documentTitle"] = document.Title,
                    ["source"] = document.Source,
                    ["sequence"] = chunk.Sequence,
                    ["startIndex"] = chunk.StartIndex,
                    ["endIndex"] = chunk.EndIndex,
                    ["chunkingStrategy"] = _chunkingStrategy.Name,
                    ["mimeType"] = document.MimeType
                }
            };

            // 合并块元数据
            foreach (var meta in chunk.Metadata)
            {
                record.Metadata[$"chunk_{meta.Key}"] = meta.Value;
            }

            records.Add(record);
            chunkIds.Add(chunk.Id);
        }

        await collection.UpsertBatchAsync(records, cancellationToken);
        _logger?.LogDebug("向量存储完成，共 {Count} 条记录", records.Count);

        // 4. 记录索引信息
        var recordInfo = new RagDocumentRecord
        {
            DocumentId = document.Id,
            Title = document.Title ?? "未命名文档",
            ChunkCount = chunks.Count,
            IndexedAt = DateTimeOffset.UtcNow,
            ChunkIds = chunkIds
        };

        lock (_documentIndex)
        {
            _documentIndex[document.Id] = recordInfo;
        }

        _logger?.LogInformation("文档索引完成: {DocumentId}, 生成 {ChunkCount} 个块",
            document.Id, chunks.Count);

        return recordInfo;
    }

    public async Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        string query,
        RetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RetrievalOptions();

        _logger?.LogDebug("开始检索查询: {Query}, TopK: {TopK}", query, options.TopK);

        // 1. 生成查询 Embedding
        var queryEmbedding = await _embeddingService.EmbedAsync(query, cancellationToken);

        // 2. 向量搜索
        var collection = await _vectorStore.GetOrCreateCollectionAsync(
            "rag_documents",
            _embeddingService.Dimensions,
            cancellationToken);

        var searchOptions = new VectorSearchOptions
        {
            TopK = options.TopK,
            MinSimilarity = options.MinSimilarity,
            DistanceMetric = options.DistanceMetric,
            Filter = options.Filter
        };

        var results = await collection.SearchAsync(queryEmbedding, searchOptions, cancellationToken);

        // 3. 转换为 RAG 搜索结果
        var ragResults = results.Select(r => new RagSearchResult
        {
            Chunk = new Chunk
            {
                Id = r.Record.Id,
                DocumentId = GetGuidMetadata(r.Record.Metadata, "documentId"),
                Text = r.Record.Text ?? string.Empty,
                StartIndex = GetIntMetadata(r.Record.Metadata, "startIndex"),
                EndIndex = GetIntMetadata(r.Record.Metadata, "endIndex"),
                Sequence = GetIntMetadata(r.Record.Metadata, "sequence"),
                Metadata = r.Record.Metadata
                    .Where(m => m.Key.StartsWith("chunk_"))
                    .ToDictionary(
                        m => m.Key[6..],
                        m => m.Value)
            },
            Score = r.Score,
            Document = options.IncludeDocument ? ReconstructDocument(r.Record) : null
        }).ToList();

        _logger?.LogDebug("检索完成，返回 {Count} 个结果", ragResults.Count);
        return ragResults;
    }

    public async Task<bool> DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("删除文档索引: {DocumentId}", documentId);

        lock (_documentIndex)
        {
            if (!_documentIndex.TryGetValue(documentId, out var record))
            {
                _logger?.LogWarning("文档索引不存在: {DocumentId}", documentId);
                return false;
            }
        }

        var collection = await _vectorStore.GetOrCreateCollectionAsync(
            "rag_documents",
            _embeddingService.Dimensions,
            cancellationToken);

        // 删除所有关联的块
        RagDocumentRecord? docRecord;
        lock (_documentIndex)
        {
            _documentIndex.TryGetValue(documentId, out docRecord);
        }

        if (docRecord != null)
        {
            foreach (var chunkId in docRecord.ChunkIds)
            {
                await collection.DeleteAsync(chunkId, cancellationToken);
            }
        }

        lock (_documentIndex)
        {
            _documentIndex.Remove(documentId);
        }

        _logger?.LogInformation("文档索引删除完成: {DocumentId}", documentId);
        return true;
    }

    private static IDocument? ReconstructDocument(VectorRecord record)
    {
        var docId = GetGuidMetadata(record.Metadata, "documentId");
        if (docId == Guid.Empty)
            return null;

        return new Document
        {
            Id = docId,
            Title = GetStringMetadata(record.Metadata, "documentTitle"),
            Content = record.Text ?? string.Empty,
            Source = GetStringMetadata(record.Metadata, "source"),
            MimeType = GetStringMetadata(record.Metadata, "mimeType")
        };
    }

    private static Guid GetGuidMetadata(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
            return Guid.Empty;

        return value switch
        {
            Guid guid => guid,
            string str when Guid.TryParse(str, out var guid) => guid,
            JsonElement { ValueKind: JsonValueKind.String } json when Guid.TryParse(json.GetString(), out var guid) => guid,
            _ => Guid.Empty
        };
    }

    private static int GetIntMetadata(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
            return 0;

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            string str when int.TryParse(str, out var intValue) => intValue,
            JsonElement { ValueKind: JsonValueKind.Number } json when json.TryGetInt32(out var intValue) => intValue,
            _ => 0
        };
    }

    private static string? GetStringMetadata(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
            return null;

        return value switch
        {
            string str => str,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            _ => value.ToString()
        };
    }
}
