using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

internal sealed class PostgresDocumentService : IDocumentService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<PostgresDocumentService>? _logger;

    public PostgresDocumentService(
        QiaKonAppDbContext dbContext,
        IHostEnvironment hostEnvironment,
        ILogger<PostgresDocumentService>? logger = null)
    {
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public DocumentPagedResultDto GetDocuments(
        int page,
        int pageSize,
        Guid? departmentId = null,
        IndexStatus? status = null,
        string? searchTitle = null,
        string sortBy = "createdAt",
        bool sortDescending = true)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.Documents.AsNoTracking().AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(d => d.DepartmentId == departmentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.IndexStatus == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTitle))
        {
            query = query.Where(d => d.Title.Contains(searchTitle));
        }

        query = sortBy.ToLowerInvariant() switch
        {
            "title" => sortDescending ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            "size" => sortDescending ? query.OrderByDescending(d => d.Size) : query.OrderBy(d => d.Size),
            _ => sortDescending ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt),
        };

        var totalCount = query.LongCount();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .Select(ToListItemDto)
            .ToList();

        return new DocumentPagedResultDto(items, totalCount, page, pageSize);
    }

    public DocumentDetailDto? GetDocument(Guid id)
    {
        var document = _dbContext.Documents.AsNoTracking().FirstOrDefault(d => d.Id == id);
        return document is null ? null : ToDetailDto(document);
    }

    public DocumentDetailWithChunksDto? GetDocumentWithChunks(Guid id)
    {
        var document = _dbContext.Documents.AsNoTracking().FirstOrDefault(d => d.Id == id);
        if (document is null)
        {
            return null;
        }

        var chunks = _dbContext.DocumentChunks.AsNoTracking()
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.Order)
            .Select(c => new ChunkInfoDto(c.Id, c.Order, c.Content, c.ChunkingStrategy, c.CreatedAt))
            .ToList();

        return new DocumentDetailWithChunksDto(
            document.Id,
            document.Title,
            document.Content,
            document.Type,
            document.DepartmentId,
            QiaKonSeedData.GetDepartmentName(document.DepartmentId),
            document.AccessLevel,
            document.IndexStatus,
            document.Version,
            document.IndexVersion,
            document.Size,
            ParseJson(document.MetadataJson),
            document.CreatedAt,
            document.CreatedBy,
            document.ModifiedAt,
            document.ModifiedBy,
            chunks);
    }

    public DocumentDetailDto CreateDocument(CreateDocumentRequestDto request, Guid userId)
    {
        var document = new DocumentRow
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            DepartmentId = request.DepartmentId,
            AccessLevel = request.AccessLevel,
            IndexStatus = IndexStatus.Pending,
            Version = 1,
            IndexVersion = null,
            MetadataJson = SerializeJson(request.Metadata),
            Size = request.Content.Length,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            IndexProgress = 0,
        };

        _dbContext.Documents.Add(document);
        _dbContext.DocumentChunks.AddRange(GenerateSimulatedChunks(document.Id, document.Content ?? string.Empty));
        _dbContext.SaveChanges();

        return ToDetailDto(document);
    }

    public async Task<DocumentDetailDto> UploadDocumentAsync(IFormFile file, UploadDocumentFormDto form, Guid userId, CancellationToken cancellationToken = default)
    {
        var uploadsRoot = EnsureUploadDirectory();
        var storedFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(uploadsRoot, storedFileName);
        var relativePath = Path.Combine("uploads", storedFileName).Replace('\\', '/');

        await using (var output = File.Create(fullPath))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        string content;
        if (IsPlainText(file.FileName, file.ContentType))
        {
            content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        }
        else
        {
            content = $"已上传文件 {file.FileName}，大小 {file.Length} 字节。该文件已登记到知识库，可在后续接入真实解析器后处理。";
        }

        var metadata = new JsonObject
        {
            ["originalFileName"] = file.FileName,
            ["contentType"] = file.ContentType,
            ["description"] = form.Description,
            ["visibility"] = form.Visibility,
        };

        var document = new DocumentRow
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(form.Title) ? Path.GetFileNameWithoutExtension(file.FileName) : form.Title,
            Content = content,
            Type = GetDocumentType(file.FileName),
            DepartmentId = form.DepartmentId ?? QiaKonSeedData.GetDefaultDepartmentId(),
            AccessLevel = QiaKonSeedData.ResolveAccessLevel(form.AccessLevel, form.Visibility),
            IndexStatus = IndexStatus.Completed,
            Version = 1,
            IndexVersion = 1,
            MetadataJson = SerializeJson(metadata),
            Size = file.Length,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            FilePath = relativePath,
            IndexProgress = 100,
            IndexCompletedAt = DateTime.UtcNow,
        };

        _dbContext.Documents.Add(document);
        _dbContext.DocumentChunks.AddRange(GenerateSimulatedChunks(document.Id, content));
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Document stored in PostgreSQL: {DocumentId} ({Title})", document.Id, document.Title);
        return ToDetailDto(document);
    }

    public DocumentDetailDto? UpdateDocument(Guid id, UpdateDocumentRequestDto request, Guid userId)
    {
        var document = _dbContext.Documents.FirstOrDefault(d => d.Id == id);
        if (document is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            document.Title = request.Title;
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            document.Content = request.Content;
            document.Size = request.Content.Length;
        }

        if (request.AccessLevel.HasValue)
        {
            document.AccessLevel = request.AccessLevel.Value;
        }

        if (request.Metadata is not null)
        {
            document.MetadataJson = SerializeJson(request.Metadata);
        }

        document.Version++;
        document.IndexStatus = IndexStatus.Pending;
        document.IndexVersion = null;
        document.IndexProgress = 0;
        document.IndexStartedAt = null;
        document.IndexCompletedAt = null;
        document.IndexErrorMessage = null;
        document.ModifiedBy = userId;
        document.ModifiedAt = DateTime.UtcNow;

        _dbContext.SaveChanges();
        return ToDetailDto(document);
    }

    public bool DeleteDocument(Guid id)
    {
        var document = _dbContext.Documents.FirstOrDefault(d => d.Id == id);
        if (document is null)
        {
            return false;
        }

        var chunks = _dbContext.DocumentChunks.Where(c => c.DocumentId == id).ToList();
        if (chunks.Count > 0)
        {
            _dbContext.DocumentChunks.RemoveRange(chunks);
        }

        _dbContext.Documents.Remove(document);
        _dbContext.SaveChanges();

        DeleteStoredFile(document.FilePath);
        return true;
    }

    public BatchDeleteResponseDto BatchDeleteDocuments(IReadOnlyList<Guid> documentIds)
    {
        var deletedCount = 0;
        var failedIds = new List<Guid>();

        foreach (var documentId in documentIds)
        {
            if (DeleteDocument(documentId))
            {
                deletedCount++;
            }
            else
            {
                failedIds.Add(documentId);
            }
        }

        return new BatchDeleteResponseDto(deletedCount, failedIds.Count, failedIds);
    }

    public (string FilePath, string FileName)? GetDocumentDownloadInfo(Guid id)
    {
        var document = _dbContext.Documents.AsNoTracking().FirstOrDefault(d => d.Id == id);
        if (document is null)
        {
            return null;
        }

        var metadata = ParseJson(document.MetadataJson);
        var fileName = metadata?["originalFileName"]?.GetValue<string>() ?? $"{document.Title}{GetExtension(document.Type)}";
        var filePath = string.IsNullOrWhiteSpace(document.FilePath)
            ? $"documents/{id}/{fileName}"
            : document.FilePath;

        return (filePath, fileName);
    }

    public ReindexResponseDto Reindex(Guid? documentId)
    {
        var documents = documentId.HasValue
            ? _dbContext.Documents.Where(d => d.Id == documentId.Value).ToList()
            : _dbContext.Documents.ToList();

        foreach (var document in documents)
        {
            document.IndexStatus = IndexStatus.Completed;
            document.IndexVersion = (document.IndexVersion ?? 0) + 1;
            document.IndexProgress = 100;
            document.IndexStartedAt ??= DateTime.UtcNow.AddSeconds(-1);
            document.IndexCompletedAt = DateTime.UtcNow;
            document.IndexErrorMessage = null;

            var hasChunks = _dbContext.DocumentChunks.Any(c => c.DocumentId == document.Id);
            if (!hasChunks)
            {
                _dbContext.DocumentChunks.AddRange(GenerateSimulatedChunks(document.Id, document.Content ?? string.Empty));
            }
        }

        _dbContext.SaveChanges();
        return new ReindexResponseDto(documents.Count, $"已重建 {documents.Count} 个文档的索引");
    }

    public IndexQueueStatusDto GetIndexQueueStatus()
    {
        var allItems = GetAllIndexQueueItems().Items;
        var pending = allItems.Where(x => x.Status == IndexStatus.Pending).ToList();
        var indexing = allItems.Where(x => x.Status == IndexStatus.Indexing).ToList();
        var completed = allItems.Where(x => x.Status == IndexStatus.Completed).ToList();
        var failed = allItems.Where(x => x.Status == IndexStatus.Failed).ToList();

        return new IndexQueueStatusDto(
            pending.Count,
            indexing.Count,
            completed.Count,
            failed.Count,
            pending,
            indexing,
            completed,
            failed);
    }

    public IndexQueueResponseDto GetAllIndexQueueItems()
    {
        var items = _dbContext.Documents.AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .ToList()
            .Select(ToIndexQueueItemDto)
            .ToList();

        return new IndexQueueResponseDto(items, items.Count);
    }

    public ReindexResponseDto RetryFailedIndexing()
    {
        var failedDocuments = _dbContext.Documents.Where(d => d.IndexStatus == IndexStatus.Failed).ToList();
        foreach (var document in failedDocuments)
        {
            document.IndexStatus = IndexStatus.Pending;
            document.IndexProgress = 0;
            document.IndexStartedAt = null;
            document.IndexCompletedAt = null;
            document.IndexErrorMessage = null;
        }

        _dbContext.SaveChanges();
        return new ReindexResponseDto(failedDocuments.Count, $"已重试 {failedDocuments.Count} 个失败任务");
    }

    public IndexStatsDto GetIndexStats()
    {
        var documents = _dbContext.Documents.AsNoTracking().ToList();
        var totalDocs = documents.LongCount();
        var totalChunks = _dbContext.DocumentChunks.LongCount();
        var completedDocs = documents.Count(d => d.IndexStatus == IndexStatus.Completed);
        var failedDocs = documents.Count(d => d.IndexStatus == IndexStatus.Failed);
        var pendingDocs = documents.Count(d => d.IndexStatus == IndexStatus.Pending);
        var indexingDocs = documents.Count(d => d.IndexStatus == IndexStatus.Indexing);
        var successRate = totalDocs > 0 ? (double)completedDocs / totalDocs : 0;
        var averageDurationSeconds = documents
            .Where(d => d.IndexStartedAt.HasValue && d.IndexCompletedAt.HasValue)
            .Select(d => (d.IndexCompletedAt!.Value - d.IndexStartedAt!.Value).TotalSeconds)
            .DefaultIfEmpty(0)
            .Average();
        var today = DateTime.UtcNow.Date;
        var completedToday = documents.Count(d => d.IndexCompletedAt.HasValue && d.IndexCompletedAt.Value >= today);
        var failedToday = documents.Count(d => d.IndexStatus == IndexStatus.Failed && (d.ModifiedAt ?? d.CreatedAt) >= today);

        return new IndexStatsDto(
            totalDocs,
            totalChunks,
            Math.Round(successRate, 2),
            Math.Round(averageDurationSeconds, 2),
            completedToday,
            failedToday,
            pendingDocs,
            indexingDocs,
            completedDocs,
            failedDocs);
    }

    public ReparseResponseDto ReparseDocument(Guid documentId, string? chunkingStrategy = null)
    {
        var document = _dbContext.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
        {
            return new ReparseResponseDto(documentId, "文档不存在", 0);
        }

        var existingChunks = _dbContext.DocumentChunks.Where(c => c.DocumentId == documentId).ToList();
        if (existingChunks.Count > 0)
        {
            _dbContext.DocumentChunks.RemoveRange(existingChunks);
        }

        var newChunks = GenerateSimulatedChunks(documentId, document.Content ?? string.Empty, chunkingStrategy);
        _dbContext.DocumentChunks.AddRange(newChunks);

        document.IndexStatus = IndexStatus.Pending;
        document.IndexVersion = null;
        document.IndexProgress = 0;
        document.IndexStartedAt = null;
        document.IndexCompletedAt = null;
        document.IndexErrorMessage = null;
        document.ModifiedAt = DateTime.UtcNow;

        _dbContext.SaveChanges();
        return new ReparseResponseDto(documentId, "文档已重新解析", newChunks.Count);
    }

    private DocumentListItemDto ToListItemDto(DocumentRow document)
        => new(
            document.Id,
            document.Title,
            document.Type,
            document.DepartmentId,
            QiaKonSeedData.GetDepartmentName(document.DepartmentId),
            document.AccessLevel,
            document.IndexStatus,
            document.Version,
            document.CreatedAt,
            document.ModifiedAt,
            document.CreatedBy,
            document.Size,
            ParseJson(document.MetadataJson));

    private DocumentDetailDto ToDetailDto(DocumentRow document)
        => new(
            document.Id,
            document.Title,
            document.Content,
            document.Type,
            document.DepartmentId,
            QiaKonSeedData.GetDepartmentName(document.DepartmentId),
            document.AccessLevel,
            document.IndexStatus,
            document.Version,
            document.IndexVersion,
            document.Size,
            ParseJson(document.MetadataJson),
            document.CreatedAt,
            document.CreatedBy,
            document.ModifiedAt,
            document.ModifiedBy);

    private IndexQueueItemDto ToIndexQueueItemDto(DocumentRow document)
        => new(
            document.Id,
            document.Title,
            document.IndexStatus,
            document.IndexProgress,
            document.IndexStartedAt,
            document.IndexCompletedAt,
            document.IndexErrorMessage,
            document.CreatedAt);

    private List<DocumentChunkRow> GenerateSimulatedChunks(Guid documentId, string content, string? chunkingStrategy = null)
    {
        var strategy = string.IsNullOrWhiteSpace(chunkingStrategy) ? "RecursiveCharacterTextSplitter" : chunkingStrategy;
        var chunks = new List<DocumentChunkRow>();
        var sentences = content.Split(['。', '；', '\n', '！', '？'], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < sentences.Length; i++)
        {
            var sentence = sentences[i].Trim();
            if (string.IsNullOrWhiteSpace(sentence))
            {
                continue;
            }

            chunks.Add(new DocumentChunkRow
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Content = sentence.Length > 200 ? sentence[..200] : sentence,
                Order = i + 1,
                ChunkingStrategy = strategy,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (chunks.Count == 0)
        {
            chunks.Add(new DocumentChunkRow
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Content = content.Length > 200 ? content[..200] : content,
                Order = 1,
                ChunkingStrategy = strategy,
                CreatedAt = DateTime.UtcNow,
            });
        }

        return chunks;
    }

    private string EnsureUploadDirectory()
    {
        var uploadsRoot = Path.Combine(_hostEnvironment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);
        return uploadsRoot;
    }

    private void DeleteStoredFile(string? relativeFilePath)
    {
        if (string.IsNullOrWhiteSpace(relativeFilePath))
        {
            return;
        }

        var fullPath = Path.Combine(_hostEnvironment.ContentRootPath, relativeFilePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static bool IsPlainText(string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".txt" or ".md" or ".json" or ".csv" or ".xml"
            || (contentType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static DocumentType GetDocumentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".md" => DocumentType.Markdown,
            ".pdf" => DocumentType.Pdf,
            ".doc" or ".docx" => DocumentType.Word,
            ".html" or ".htm" => DocumentType.Html,
            _ => DocumentType.PlainText,
        };

    private static string GetExtension(DocumentType type)
        => type switch
        {
            DocumentType.Markdown => ".md",
            DocumentType.Pdf => ".pdf",
            DocumentType.Word => ".docx",
            DocumentType.Html => ".html",
            _ => ".txt",
        };

    private static string? SerializeJson(JsonObject? metadata)
        => metadata?.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static JsonObject? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonNode.Parse(json) as JsonObject;
    }
}
