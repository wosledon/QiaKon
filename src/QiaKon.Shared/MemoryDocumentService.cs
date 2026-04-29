using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;
using System.Text.Json.Nodes;

namespace QiaKon.Shared;

/// <summary>
/// 内存态文档服务实现（带种子数据）
/// </summary>
public sealed class MemoryDocumentService : IDocumentService
{
    private readonly Dictionary<Guid, DocumentRecord> _documents = new();
    private readonly Dictionary<Guid, DepartmentInfo> _departments = new();
    private readonly Dictionary<Guid, List<ChunkRecord>> _chunks = new();
    private readonly Dictionary<Guid, string> _filePaths = new();
    private readonly Dictionary<Guid, IndexProgressRecord> _indexProgress = new();
    private readonly ILogger<MemoryDocumentService>? _logger;

    public MemoryDocumentService(ILogger<MemoryDocumentService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        var deptEngineering = new DepartmentInfo { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "研发部" };
        var deptSales = new DepartmentInfo { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "销售部" };
        var deptHR = new DepartmentInfo { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "人力资源部" };
        var deptAdmin = new DepartmentInfo { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "行政部" };

        _departments[deptEngineering.Id] = deptEngineering;
        _departments[deptSales.Id] = deptSales;
        _departments[deptHR.Id] = deptHR;
        _departments[deptAdmin.Id] = deptAdmin;

        var adminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var engineerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        var seedDocs = new[]
        {
            new DocumentRecord
            {
                Id = Guid.Parse("d1111111-1111-1111-1111-111111111111"),
                Title = "QiaKon平台架构设计文档",
                Content = "QiaKon是一个企业级KAG平台，将知识图谱的结构化推理能力与RAG的灵活检索能力深度融合。平台采用模块化架构，包括API层、服务层、数据层等多个组件。核心技术栈包括：.NET 9、ASP.NET Core、EF Core、PostgreSQL、Redis等。",
                Type = DocumentType.Markdown,
                DepartmentId = deptEngineering.Id,
                AccessLevel = AccessLevel.Department,
                IndexStatus = IndexStatus.Completed,
                Version = 1,
                IndexVersion = 1,
                Size = 4096,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                Metadata = new JsonObject { ["author"] = "系统管理员", ["tags"] = new JsonArray("架构", "设计") }
            },
            new DocumentRecord
            {
                Id = Guid.Parse("d2222222-2222-2222-2222-222222222222"),
                Title = "RAG检索管道技术方案",
                Content = "RAG（检索增强生成）管道包含以下关键组件：文档解析器支持PDF、Word、Markdown等格式；分块策略支持语义分块、递归分块、固定长度分块；嵌入服务生成文档块的向量表示；向量存储使用pgvector。",
                Type = DocumentType.Markdown,
                DepartmentId = deptEngineering.Id,
                AccessLevel = AccessLevel.Confidential,
                IndexStatus = IndexStatus.Completed,
                Version = 2,
                IndexVersion = 1,
                Size = 3584,
                CreatedBy = engineerId,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                Metadata = new JsonObject { ["author"] = "工程师", ["tags"] = new JsonArray("RAG", "检索") }
            },
            new DocumentRecord
            {
                Id = Guid.Parse("d3333333-3333-3333-3333-333333333333"),
                Title = "知识图谱引擎设计文档",
                Content = "知识图谱引擎支持内存与Npgsql两种存储后端，提供实体管理、关系管理、路径查询、多跳推理等能力。实体属性支持JSON格式。",
                Type = DocumentType.Markdown,
                DepartmentId = deptEngineering.Id,
                AccessLevel = AccessLevel.Public,
                IndexStatus = IndexStatus.Completed,
                Version = 1,
                IndexVersion = 1,
                Size = 2048,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                Metadata = new JsonObject { ["author"] = "系统管理员", ["tags"] = new JsonArray("图谱", "引擎") }
            },
            new DocumentRecord
            {
                Id = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
                Title = "公司年度销售报告2025",
                Content = "2025年度销售报告：全年销售额同比增长25%，达到5亿元人民币。新增客户200家，重点行业突破包括金融、医疗、制造三大领域。",
                Type = DocumentType.Pdf,
                DepartmentId = deptSales.Id,
                AccessLevel = AccessLevel.Restricted,
                IndexStatus = IndexStatus.Completed,
                Version = 1,
                IndexVersion = 1,
                Size = 5120,
                CreatedBy = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                Metadata = new JsonObject { ["author"] = "销售部", ["department"] = "销售部" }
            },
            new DocumentRecord
            {
                Id = Guid.Parse("d5555555-5555-5555-5555-555555555555"),
                Title = "员工手册",
                Content = "欢迎加入QiaKon公司！本手册包含公司制度、福利政策、考勤规定等内容。所有员工入职后需完成岗前培训。",
                Type = DocumentType.Markdown,
                DepartmentId = deptHR.Id,
                AccessLevel = AccessLevel.Public,
                IndexStatus = IndexStatus.Pending,
                Version = 1,
                Size = 1536,
                CreatedBy = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Metadata = new JsonObject { ["author"] = "人力资源部" }
            },
            new DocumentRecord
            {
                Id = Guid.Parse("d6666666-6666-6666-6666-666666666666"),
                Title = "研发部项目管理制度",
                Content = "研发部项目管理规范：代码必须经过review才能合并；单元测试覆盖率需达到80%以上；重要功能必须编写技术文档；发布流程遵循语义化版本规范。",
                Type = DocumentType.Markdown,
                DepartmentId = deptEngineering.Id,
                AccessLevel = AccessLevel.Department,
                IndexStatus = IndexStatus.Indexing,
                Version = 3,
                IndexVersion = 2,
                Size = 1920,
                CreatedBy = engineerId,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Metadata = new JsonObject { ["author"] = "工程师", ["tags"] = new JsonArray("管理", "制度") }
            },
            new DocumentRecord
            {
                Id = Guid.Parse("d7777777-7777-7777-7777-777777777777"),
                Title = "市场推广方案2025",
                Content = "2025年市场推广方案包括线上渠道扩展、线下活动策划、品牌合作等内容。重点投入数字营销领域。",
                Type = DocumentType.Word,
                DepartmentId = deptSales.Id,
                AccessLevel = AccessLevel.Department,
                IndexStatus = IndexStatus.Failed,
                Version = 1,
                Size = 2816,
                CreatedBy = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Metadata = new JsonObject { ["author"] = "市场部" }
            },
            new DocumentRecord
            {
                Id = Guid.Parse("d8888888-8888-8888-8888-888888888888"),
                Title = "新产品功能规划",
                Content = "下一代产品功能规划：增强型知识推理、多模态检索、高级可视化分析、自动化工作流等核心功能。",
                Type = DocumentType.Markdown,
                DepartmentId = deptEngineering.Id,
                AccessLevel = AccessLevel.Restricted,
                IndexStatus = IndexStatus.Pending,
                Version = 1,
                Size = 1792,
                CreatedBy = engineerId,
                CreatedAt = DateTime.UtcNow.AddHours(-6),
                Metadata = new JsonObject { ["author"] = "产品经理", ["tags"] = new JsonArray("产品", "规划") }
            },
        };

        foreach (var doc in seedDocs)
        {
            _documents[doc.Id] = doc;
            _chunks[doc.Id] = GenerateSimulatedChunks(doc);
            _indexProgress[doc.Id] = new IndexProgressRecord
            {
                DocumentId = doc.Id,
                Status = doc.IndexStatus,
                Progress = doc.IndexStatus == IndexStatus.Completed ? 100 : (doc.IndexStatus == IndexStatus.Indexing ? 50 : 0),
                StartedAt = doc.IndexStatus == IndexStatus.Indexing ? DateTime.UtcNow.AddMinutes(-5) : null,
                CompletedAt = doc.IndexStatus == IndexStatus.Completed ? DateTime.UtcNow.AddDays(-1) : null
            };
        }

        _logger?.LogInformation("MemoryDocumentService initialized with {DocCount} documents", _documents.Count);
    }

    private static List<ChunkRecord> GenerateSimulatedChunks(DocumentRecord doc)
    {
        var chunks = new List<ChunkRecord>();
        var sentences = doc.Content.Split('。', '；', '\n', '！', '？');
        for (int i = 0; i < sentences.Length; i++)
        {
            var sentence = sentences[i].Trim();
            if (string.IsNullOrWhiteSpace(sentence)) continue;
            chunks.Add(new ChunkRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                Content = sentence.Length > 200 ? sentence.Substring(0, 200) : sentence,
                Order = i + 1,
                ChunkingStrategy = "RecursiveCharacterTextSplitter",
                CreatedAt = DateTime.UtcNow
            });
        }
        if (chunks.Count == 0)
        {
            chunks.Add(new ChunkRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                Content = doc.Content.Length > 200 ? doc.Content.Substring(0, 200) : doc.Content,
                Order = 1,
                ChunkingStrategy = "RecursiveCharacterTextSplitter",
                CreatedAt = DateTime.UtcNow
            });
        }
        return chunks;
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
        var query = _documents.Values.AsEnumerable();

        if (departmentId.HasValue)
            query = query.Where(d => d.DepartmentId == departmentId.Value);

        if (status.HasValue)
            query = query.Where(d => d.IndexStatus == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTitle))
            query = query.Where(d => d.Title.Contains(searchTitle, StringComparison.OrdinalIgnoreCase));

        query = sortBy.ToLowerInvariant() switch
        {
            "title" => sortDescending ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            "size" => sortDescending ? query.OrderByDescending(d => d.Size) : query.OrderBy(d => d.Size),
            _ => sortDescending ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt)
        };

        var totalCount = query.LongCount();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToListItemDto)
            .ToList();

        return new DocumentPagedResultDto(items, totalCount, page, pageSize);
    }

    public DocumentDetailDto? GetDocument(Guid id)
    {
        return _documents.TryGetValue(id, out var doc) ? ToDetailDto(doc) : null;
    }

    public DocumentDetailWithChunksDto? GetDocumentWithChunks(Guid id)
    {
        if (!_documents.TryGetValue(id, out var doc))
            return null;

        var chunks = _chunks.TryGetValue(id, out var chunkList)
            ? chunkList.Select(c => new ChunkInfoDto(c.Id, c.Order, c.Content, c.ChunkingStrategy, c.CreatedAt)).ToList()
            : new List<ChunkInfoDto>();

        return new DocumentDetailWithChunksDto(
            doc.Id,
            doc.Title,
            doc.Content,
            doc.Type,
            doc.DepartmentId,
            _departments.GetValueOrDefault(doc.DepartmentId)?.Name ?? "未知部门",
            doc.AccessLevel,
            doc.IndexStatus,
            doc.Version,
            doc.IndexVersion,
            doc.Size,
            doc.Metadata?.DeepClone() as JsonObject,
            doc.CreatedAt,
            doc.CreatedBy,
            doc.ModifiedAt,
            doc.ModifiedBy,
            chunks);
    }

    public DocumentDetailDto CreateDocument(CreateDocumentRequestDto request, Guid userId)
    {
        var record = new DocumentRecord
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
            Size = request.Content.Length,
            Metadata = request.Metadata?.DeepClone() as JsonObject,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

        _documents[record.Id] = record;
        _chunks[record.Id] = GenerateSimulatedChunks(record);
        _indexProgress[record.Id] = new IndexProgressRecord { DocumentId = record.Id, Status = IndexStatus.Pending, Progress = 0 };
        return ToDetailDto(record);
    }

    public async Task<DocumentDetailDto> UploadDocumentAsync(IFormFile file, UploadDocumentFormDto form, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var content = IsPlainText(file.FileName, file.ContentType)
            ? await reader.ReadToEndAsync()
            : $"已上传文件 {file.FileName}，大小 {file.Length} 字节。该文件已登记到知识库，可在后续接入真实解析器后处理。";

        cancellationToken.ThrowIfCancellationRequested();

        var metadata = new JsonObject
        {
            ["originalFileName"] = file.FileName,
            ["contentType"] = file.ContentType,
            ["description"] = form.Description,
            ["visibility"] = form.Visibility,
        };

        // Resolve AccessLevel: prefer explicit AccessLevel, fallback to Visibility mapping
        var accessLevel = form.AccessLevel;
        if (!accessLevel.HasValue && !string.IsNullOrWhiteSpace(form.Visibility))
        {
            accessLevel = form.Visibility.ToLowerInvariant() switch
            {
                "public" => AccessLevel.Public,
                "department" => AccessLevel.Department,
                "private" => AccessLevel.Restricted,
                _ => AccessLevel.Department
            };
        }
        accessLevel ??= AccessLevel.Department;

        var created = CreateDocument(
            new CreateDocumentRequestDto(
                Title: string.IsNullOrWhiteSpace(form.Title) ? Path.GetFileNameWithoutExtension(file.FileName) : form.Title,
                Content: content,
                Type: GetDocumentType(file.FileName),
                DepartmentId: form.DepartmentId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
                AccessLevel: accessLevel.Value,
                Metadata: metadata),
            userId);

        if (_documents.TryGetValue(created.Id, out var record))
        {
            record.IndexStatus = IndexStatus.Completed;
            record.IndexVersion = 1;
            record.Size = file.Length;
            _documents[record.Id] = record;

            if (_indexProgress.TryGetValue(record.Id, out var progress))
            {
                progress.Status = IndexStatus.Completed;
                progress.Progress = 100;
                progress.CompletedAt = DateTime.UtcNow;
            }

            var filePath = $"uploads/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            _filePaths[record.Id] = filePath;
        }

        return GetDocument(created.Id)!;
    }

    public DocumentDetailDto? UpdateDocument(Guid id, UpdateDocumentRequestDto request, Guid userId)
    {
        if (!_documents.TryGetValue(id, out var doc))
            return null;

        if (!string.IsNullOrWhiteSpace(request.Title))
            doc.Title = request.Title;
        if (!string.IsNullOrWhiteSpace(request.Content))
            doc.Content = request.Content;
        if (request.AccessLevel.HasValue)
            doc.AccessLevel = request.AccessLevel.Value;
        if (request.Metadata is not null)
            doc.Metadata = request.Metadata.DeepClone() as JsonObject;

        doc.Version++;
        doc.IndexStatus = IndexStatus.Pending;
        doc.IndexVersion = null;
        doc.Size = doc.Content.Length;
        doc.ModifiedBy = userId;
        doc.ModifiedAt = DateTime.UtcNow;

        if (_indexProgress.TryGetValue(id, out var progress))
        {
            progress.Status = IndexStatus.Pending;
            progress.Progress = 0;
            progress.StartedAt = null;
            progress.CompletedAt = null;
        }

        return ToDetailDto(doc);
    }

    public bool DeleteDocument(Guid id)
    {
        _chunks.Remove(id);
        _indexProgress.Remove(id);
        _filePaths.Remove(id);
        return _documents.Remove(id);
    }

    public BatchDeleteResponseDto BatchDeleteDocuments(IReadOnlyList<Guid> documentIds)
    {
        var deletedCount = 0;
        var failedIds = new List<Guid>();

        foreach (var id in documentIds)
        {
            if (!DeleteDocument(id))
                failedIds.Add(id);
            else
                deletedCount++;
        }

        return new BatchDeleteResponseDto(deletedCount, failedIds.Count, failedIds);
    }

    public (string FilePath, string FileName)? GetDocumentDownloadInfo(Guid id)
    {
        if (!_documents.TryGetValue(id, out var doc))
            return null;

        var fileName = doc.Metadata?["originalFileName"]?.GetValue<string>() ?? $"{doc.Title}.{GetExtension(doc.Type)}";
        var filePath = _filePaths.TryGetValue(id, out var path) ? path : $"documents/{id}/{fileName}";
        return (filePath, fileName);
    }

    public ReindexResponseDto Reindex(Guid? documentId)
    {
        var count = 0;

        if (documentId.HasValue)
        {
            if (_documents.TryGetValue(documentId.Value, out var doc))
            {
                doc.IndexStatus = IndexStatus.Completed;
                doc.IndexVersion = (doc.IndexVersion ?? 0) + 1;
                if (_indexProgress.TryGetValue(doc.Id, out var progress))
                {
                    progress.Status = IndexStatus.Completed;
                    progress.Progress = 100;
                    progress.CompletedAt = DateTime.UtcNow;
                }
                count = 1;
            }
        }
        else
        {
            foreach (var doc in _documents.Values)
            {
                doc.IndexStatus = IndexStatus.Completed;
                doc.IndexVersion = (doc.IndexVersion ?? 0) + 1;
                if (_indexProgress.TryGetValue(doc.Id, out var progress))
                {
                    progress.Status = IndexStatus.Completed;
                    progress.Progress = 100;
                    progress.CompletedAt = DateTime.UtcNow;
                }
                count++;
            }
        }

        return new ReindexResponseDto(count, $"已重建 {count} 个文档的索引");
    }

    public IndexQueueStatusDto GetIndexQueueStatus()
    {
        var pending = new List<IndexQueueItemDto>();
        var indexing = new List<IndexQueueItemDto>();
        var failed = new List<IndexQueueItemDto>();
        var completed = new List<IndexQueueItemDto>();

        foreach (var doc in _documents.Values)
        {
            var progress = _indexProgress.GetValueOrDefault(doc.Id);
            var item = new IndexQueueItemDto(
                doc.Id,
                doc.Title,
                progress?.Status ?? doc.IndexStatus,
                progress?.Progress ?? 0,
                progress?.StartedAt,
                progress?.CompletedAt,
                progress?.ErrorMessage,
                doc.CreatedAt);

            switch (doc.IndexStatus)
            {
                case IndexStatus.Pending: pending.Add(item); break;
                case IndexStatus.Indexing: indexing.Add(item); break;
                case IndexStatus.Failed: failed.Add(item); break;
                case IndexStatus.Completed: completed.Add(item); break;
            }
        }

        return new IndexQueueStatusDto(pending.Count, indexing.Count, completed.Count, failed.Count, pending, indexing, completed, failed);
    }

    public IndexQueueResponseDto GetAllIndexQueueItems()
    {
        var allItems = new List<IndexQueueItemDto>();

        foreach (var doc in _documents.Values)
        {
            var progress = _indexProgress.GetValueOrDefault(doc.Id);
            var item = new IndexQueueItemDto(
                doc.Id,
                doc.Title,
                progress?.Status ?? doc.IndexStatus,
                progress?.Progress ?? 0,
                progress?.StartedAt,
                progress?.CompletedAt,
                progress?.ErrorMessage,
                doc.CreatedAt);
            allItems.Add(item);
        }

        return new IndexQueueResponseDto(allItems, allItems.Count);
    }

    public ReindexResponseDto RetryFailedIndexing()
    {
        var failedDocs = _documents.Values.Where(d => d.IndexStatus == IndexStatus.Failed).ToList();
        foreach (var doc in failedDocs)
        {
            doc.IndexStatus = IndexStatus.Pending;
            if (_indexProgress.TryGetValue(doc.Id, out var progress))
            {
                progress.Status = IndexStatus.Pending;
                progress.Progress = 0;
                progress.ErrorMessage = null;
            }
        }
        return new ReindexResponseDto(failedDocs.Count, $"已重试 {failedDocs.Count} 个失败任务");
    }

    public IndexStatsDto GetIndexStats()
    {
        var totalDocs = _documents.Values.LongCount();
        var totalChunks = _chunks.Values.Sum(c => c.Count);
        var completedDocs = _documents.Values.Count(d => d.IndexStatus == IndexStatus.Completed);
        var failedDocs = _documents.Values.Count(d => d.IndexStatus == IndexStatus.Failed);
        var pendingCount = _documents.Values.Count(d => d.IndexStatus == IndexStatus.Pending);
        var indexingCount = _documents.Values.Count(d => d.IndexStatus == IndexStatus.Indexing);
        var successRate = totalDocs > 0 ? (double)completedDocs / totalDocs : 0;
        var completedToday = _documents.Values.Count(d =>
            d.IndexStatus == IndexStatus.Completed &&
            d.ModifiedAt >= DateTime.UtcNow.Date);

        return new IndexStatsDto(
            totalDocs,
            totalChunks,
            Math.Round(successRate, 2),
            2.5,
            completedToday,
            failedDocs,
            pendingCount,
            indexingCount,
            completedDocs,
            failedDocs);
    }

    public ReparseResponseDto ReparseDocument(Guid documentId, string? chunkingStrategy = null)
    {
        if (!_documents.TryGetValue(documentId, out var doc))
            return new ReparseResponseDto(documentId, "文档不存在", 0);

        var newChunks = GenerateSimulatedChunks(doc);
        _chunks[documentId] = newChunks;

        doc.IndexStatus = IndexStatus.Pending;
        doc.IndexVersion = null;
        if (_indexProgress.TryGetValue(documentId, out var progress))
        {
            progress.Status = IndexStatus.Pending;
            progress.Progress = 0;
            progress.ErrorMessage = null;
        }

        return new ReparseResponseDto(documentId, "文档已重新解析", newChunks.Count);
    }

    private DocumentListItemDto ToListItemDto(DocumentRecord doc)
    {
        return new DocumentListItemDto(
            doc.Id,
            doc.Title,
            doc.Type,
            doc.DepartmentId,
            _departments.GetValueOrDefault(doc.DepartmentId)?.Name ?? "未知部门",
            doc.AccessLevel,
            doc.IndexStatus,
            doc.Version,
            doc.CreatedAt,
            doc.ModifiedAt,
            doc.CreatedBy,
            doc.Size,
            doc.Metadata?.DeepClone() as JsonObject);
    }

    private DocumentDetailDto ToDetailDto(DocumentRecord doc)
    {
        return new DocumentDetailDto(
            doc.Id,
            doc.Title,
            doc.Content,
            doc.Type,
            doc.DepartmentId,
            _departments.GetValueOrDefault(doc.DepartmentId)?.Name ?? "未知部门",
            doc.AccessLevel,
            doc.IndexStatus,
            doc.Version,
            doc.IndexVersion,
            doc.Size,
            doc.Metadata?.DeepClone() as JsonObject,
            doc.CreatedAt,
            doc.CreatedBy,
            doc.ModifiedAt,
            doc.ModifiedBy);
    }

    private static bool IsPlainText(string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".txt" or ".md" or ".json" or ".csv" or ".xml"
            || (contentType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static DocumentType GetDocumentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".md" => DocumentType.Markdown,
            ".pdf" => DocumentType.Pdf,
            ".doc" or ".docx" => DocumentType.Word,
            ".html" or ".htm" => DocumentType.Html,
            _ => DocumentType.PlainText,
        };
    }

    private static string GetExtension(DocumentType type)
    {
        return type switch
        {
            DocumentType.Markdown => ".md",
            DocumentType.Pdf => ".pdf",
            DocumentType.Word => ".docx",
            DocumentType.Html => ".html",
            _ => ".txt"
        };
    }

    private sealed class DepartmentInfo
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
    }

    private sealed class DocumentRecord
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public DocumentType Type { get; set; }
        public Guid DepartmentId { get; set; }
        public AccessLevel AccessLevel { get; set; }
        public IndexStatus IndexStatus { get; set; }
        public int Version { get; set; }
        public int? IndexVersion { get; set; }
        public JsonObject? Metadata { get; set; }
        public long Size { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    private sealed class ChunkRecord
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public required string Content { get; set; }
        public int Order { get; set; }
        public string? ChunkingStrategy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class IndexProgressRecord
    {
        public Guid DocumentId { get; set; }
        public IndexStatus Status { get; set; }
        public double? Progress { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
