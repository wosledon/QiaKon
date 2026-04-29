using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;

namespace QiaKon.Shared;

internal sealed class QiaKonDatabaseInitializer
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<QiaKonDatabaseInitializer>? _logger;

    public QiaKonDatabaseInitializer(QiaKonAppDbContext dbContext, ILogger<QiaKonDatabaseInitializer>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var seeded = false;

        if (!await _dbContext.Documents.AnyAsync(cancellationToken))
        {
            var documentSeeds = GetDocumentSeeds();
            _dbContext.Documents.AddRange(documentSeeds.Select(ToDocumentRow));
            _dbContext.DocumentChunks.AddRange(documentSeeds.SelectMany(seed => BuildChunks(seed.Id, seed.Content)));
            seeded = true;
        }

        if (!await _dbContext.GraphEntities.AnyAsync(cancellationToken))
        {
            _dbContext.GraphEntities.AddRange(GetGraphEntitySeeds());
            seeded = true;
        }

        if (!await _dbContext.GraphRelations.AnyAsync(cancellationToken))
        {
            _dbContext.GraphRelations.AddRange(GetGraphRelationSeeds());
            seeded = true;
        }

        if (seeded)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger?.LogInformation("QiaKon PostgreSQL seed initialized: {DocumentCount} documents, {EntityCount} graph entities, {RelationCount} graph relations",
                await _dbContext.Documents.CountAsync(cancellationToken),
                await _dbContext.GraphEntities.CountAsync(cancellationToken),
                await _dbContext.GraphRelations.CountAsync(cancellationToken));
        }
    }

    private static IReadOnlyList<DocumentSeedRow> GetDocumentSeeds()
        =>
        [
            new(
                Guid.Parse("d1111111-1111-1111-1111-111111111111"),
                "QiaKon平台架构设计文档",
                "QiaKon是一个企业级KAG平台，将知识图谱的结构化推理能力与RAG的灵活检索能力深度融合。平台采用模块化架构，包括API层、服务层、数据层等多个组件。核心技术栈包括：.NET 9、ASP.NET Core、EF Core、PostgreSQL、Redis等。",
                DocumentType.Markdown,
                QiaKonSeedData.EngineeringDepartmentId,
                AccessLevel.Department,
                IndexStatus.Completed,
                1,
                1,
                4096,
                QiaKonSeedData.AdminUserId,
                DateTime.UtcNow.AddDays(-30),
                JsonObject.Parse("{\"author\":\"系统管理员\",\"tags\":[\"架构\",\"设计\"]}") as JsonObject,
                null,
                100,
                null,
                DateTime.UtcNow.AddDays(-1),
                null),
            new(
                Guid.Parse("d2222222-2222-2222-2222-222222222222"),
                "RAG检索管道技术方案",
                "RAG（检索增强生成）管道包含以下关键组件：文档解析器支持PDF、Word、Markdown等格式；分块策略支持语义分块、递归分块、固定长度分块；嵌入服务生成文档块的向量表示；向量存储使用pgvector。",
                DocumentType.Markdown,
                QiaKonSeedData.EngineeringDepartmentId,
                AccessLevel.Confidential,
                IndexStatus.Completed,
                2,
                1,
                3584,
                QiaKonSeedData.EngineerUserId,
                DateTime.UtcNow.AddDays(-20),
                JsonObject.Parse("{\"author\":\"工程师\",\"tags\":[\"RAG\",\"检索\"]}") as JsonObject,
                null,
                100,
                null,
                DateTime.UtcNow.AddDays(-1),
                null),
            new(
                Guid.Parse("d3333333-3333-3333-3333-333333333333"),
                "知识图谱引擎设计文档",
                "知识图谱引擎支持内存与Npgsql两种存储后端，提供实体管理、关系管理、路径查询、多跳推理等能力。实体属性支持JSON格式。",
                DocumentType.Markdown,
                QiaKonSeedData.EngineeringDepartmentId,
                AccessLevel.Public,
                IndexStatus.Completed,
                1,
                1,
                2048,
                QiaKonSeedData.AdminUserId,
                DateTime.UtcNow.AddDays(-15),
                JsonObject.Parse("{\"author\":\"系统管理员\",\"tags\":[\"图谱\",\"引擎\"]}") as JsonObject,
                null,
                100,
                null,
                DateTime.UtcNow.AddDays(-1),
                null),
            new(
                Guid.Parse("d4444444-4444-4444-4444-444444444444"),
                "公司年度销售报告2025",
                "2025年度销售报告：全年销售额同比增长25%，达到5亿元人民币。新增客户200家，重点行业突破包括金融、医疗、制造三大领域。",
                DocumentType.Pdf,
                QiaKonSeedData.SalesDepartmentId,
                AccessLevel.Restricted,
                IndexStatus.Completed,
                1,
                1,
                5120,
                QiaKonSeedData.SalesUserId,
                DateTime.UtcNow.AddDays(-10),
                JsonObject.Parse("{\"author\":\"销售部\",\"department\":\"销售部\"}") as JsonObject,
                null,
                100,
                null,
                DateTime.UtcNow.AddDays(-1),
                null),
            new(
                Guid.Parse("d5555555-5555-5555-5555-555555555555"),
                "员工手册",
                "欢迎加入QiaKon公司！本手册包含公司制度、福利政策、考勤规定等内容。所有员工入职后需完成岗前培训。",
                DocumentType.Markdown,
                QiaKonSeedData.HrDepartmentId,
                AccessLevel.Public,
                IndexStatus.Pending,
                1,
                null,
                1536,
                QiaKonSeedData.SalesUserId,
                DateTime.UtcNow.AddDays(-5),
                JsonObject.Parse("{\"author\":\"人力资源部\"}") as JsonObject,
                null,
                0,
                null,
                null,
                null),
            new(
                Guid.Parse("d6666666-6666-6666-6666-666666666666"),
                "研发部项目管理制度",
                "研发部项目管理规范：代码必须经过review才能合并；单元测试覆盖率需达到80%以上；重要功能必须编写技术文档；发布流程遵循语义化版本规范。",
                DocumentType.Markdown,
                QiaKonSeedData.EngineeringDepartmentId,
                AccessLevel.Department,
                IndexStatus.Indexing,
                3,
                2,
                1920,
                QiaKonSeedData.EngineerUserId,
                DateTime.UtcNow.AddDays(-2),
                JsonObject.Parse("{\"author\":\"工程师\",\"tags\":[\"管理\",\"制度\"]}") as JsonObject,
                null,
                50,
                DateTime.UtcNow.AddMinutes(-5),
                null,
                null),
            new(
                Guid.Parse("d7777777-7777-7777-7777-777777777777"),
                "市场推广方案2025",
                "2025年市场推广方案包括线上渠道扩展、线下活动策划、品牌合作等内容。重点投入数字营销领域。",
                DocumentType.Word,
                QiaKonSeedData.SalesDepartmentId,
                AccessLevel.Department,
                IndexStatus.Failed,
                1,
                null,
                2816,
                QiaKonSeedData.SalesUserId,
                DateTime.UtcNow.AddDays(-1),
                JsonObject.Parse("{\"author\":\"市场部\"}") as JsonObject,
                null,
                0,
                DateTime.UtcNow.AddHours(-12),
                null,
                "模拟解析器缺少Word依赖"),
            new(
                Guid.Parse("d8888888-8888-8888-8888-888888888888"),
                "新产品功能规划",
                "下一代产品功能规划：增强型知识推理、多模态检索、高级可视化分析、自动化工作流等核心功能。",
                DocumentType.Markdown,
                QiaKonSeedData.EngineeringDepartmentId,
                AccessLevel.Restricted,
                IndexStatus.Pending,
                1,
                null,
                1792,
                QiaKonSeedData.EngineerUserId,
                DateTime.UtcNow.AddHours(-6),
                JsonObject.Parse("{\"author\":\"产品经理\",\"tags\":[\"产品\",\"规划\"]}") as JsonObject,
                null,
                0,
                null,
                null,
                null),
        ];

    private static IReadOnlyList<GraphEntityRow> GetGraphEntitySeeds()
        =>
        [
            new() { Id = "entity_001", Name = "QiaKon平台", Type = "Platform", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = true, PropertiesJson = SerializeJson(new JsonObject { ["description"] = "企业级KAG平台", ["version"] = "1.0" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-30) },
            new() { Id = "entity_002", Name = "RAG检索模块", Type = "Module", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = true, PropertiesJson = SerializeJson(new JsonObject { ["description"] = "检索增强生成模块", ["technology"] = "pgvector" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new() { Id = "entity_003", Name = "知识图谱引擎", Type = "Module", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = true, PropertiesJson = SerializeJson(new JsonObject { ["description"] = "知识图谱存储与查询引擎", ["storage"] = "Memory/Npgsql" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new() { Id = "entity_004", Name = ".NET 9", Type = "Technology", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = true, PropertiesJson = SerializeJson(new JsonObject { ["company"] = "Microsoft" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            new() { Id = "entity_005", Name = "PostgreSQL", Type = "Database", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = true, PropertiesJson = SerializeJson(new JsonObject { ["features"] = "pgvector" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            new() { Id = "entity_006", Name = "Redis", Type = "Cache", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = false, PropertiesJson = SerializeJson(new JsonObject { ["description"] = "分布式缓存" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-15) },
            new() { Id = "entity_007", Name = "张伟", Type = "Person", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = false, PropertiesJson = SerializeJson(new JsonObject { ["title"] = "研发经理", ["email"] = "zhangwei@qiakon.com" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Id = "entity_008", Name = "李娜", Type = "Person", DepartmentId = QiaKonSeedData.SalesDepartmentId, IsPublic = false, PropertiesJson = SerializeJson(new JsonObject { ["title"] = "销售总监", ["email"] = "lina@qiakon.com" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Id = "entity_009", Name = "KAG融合架构", Type = "Concept", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = true, PropertiesJson = SerializeJson(new JsonObject { ["description"] = "知识图谱与RAG深度融合架构" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new() { Id = "entity_010", Name = "向量检索", Type = "Technology", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, IsPublic = true, PropertiesJson = SerializeJson(new JsonObject { ["description"] = "基于向量相似度的检索技术" }), CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-5) },
        ];

    private static IReadOnlyList<GraphRelationRow> GetGraphRelationSeeds()
        =>
        [
            new() { Id = "rel_001", SourceId = "entity_001", TargetId = "entity_002", Type = "CONTAINS", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new() { Id = "rel_002", SourceId = "entity_001", TargetId = "entity_003", Type = "CONTAINS", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new() { Id = "rel_003", SourceId = "entity_001", TargetId = "entity_009", Type = "IMPLEMENTS", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new() { Id = "rel_004", SourceId = "entity_002", TargetId = "entity_010", Type = "USES", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new() { Id = "rel_005", SourceId = "entity_002", TargetId = "entity_005", Type = "USES", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            new() { Id = "rel_006", SourceId = "entity_003", TargetId = "entity_005", Type = "USES", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            new() { Id = "rel_007", SourceId = "entity_001", TargetId = "entity_004", Type = "BUILT_WITH", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            new() { Id = "rel_008", SourceId = "entity_001", TargetId = "entity_006", Type = "USES", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-15) },
            new() { Id = "rel_009", SourceId = "entity_007", TargetId = "entity_001", Type = "MANAGES", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Id = "rel_010", SourceId = "entity_008", TargetId = "entity_001", Type = "SUPPORTS", DepartmentId = QiaKonSeedData.SalesDepartmentId, PropertiesJson = "{}", CreatedBy = QiaKonSeedData.AdminUserId, CreatedAt = DateTime.UtcNow.AddDays(-10) },
        ];

    private static DocumentRow ToDocumentRow(DocumentSeedRow seed)
        => new()
        {
            Id = seed.Id,
            Title = seed.Title,
            Content = seed.Content,
            Type = seed.Type,
            DepartmentId = seed.DepartmentId,
            AccessLevel = seed.AccessLevel,
            IndexStatus = seed.IndexStatus,
            Version = seed.Version,
            IndexVersion = seed.IndexVersion,
            MetadataJson = SerializeJson(seed.Metadata),
            Size = seed.Size,
            CreatedBy = seed.CreatedBy,
            CreatedAt = seed.CreatedAt,
            ModifiedBy = null,
            ModifiedAt = null,
            FilePath = seed.FilePath,
            IndexProgress = seed.IndexProgress,
            IndexStartedAt = seed.IndexStartedAt,
            IndexCompletedAt = seed.IndexCompletedAt,
            IndexErrorMessage = seed.IndexErrorMessage,
        };

    private static IReadOnlyList<DocumentChunkRow> BuildChunks(Guid documentId, string content)
    {
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
                ChunkingStrategy = "RecursiveCharacterTextSplitter",
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
                ChunkingStrategy = "RecursiveCharacterTextSplitter",
                CreatedAt = DateTime.UtcNow,
            });
        }

        return chunks;
    }

    private static string? SerializeJson(JsonObject? jsonObject)
        => jsonObject?.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

internal sealed record DocumentSeedRow(
    Guid Id,
    string Title,
    string Content,
    DocumentType Type,
    Guid DepartmentId,
    AccessLevel AccessLevel,
    IndexStatus IndexStatus,
    int Version,
    int? IndexVersion,
    long Size,
    Guid CreatedBy,
    DateTime CreatedAt,
    JsonObject? Metadata,
    string? FilePath,
    double? IndexProgress,
    DateTime? IndexStartedAt,
    DateTime? IndexCompletedAt,
    string? IndexErrorMessage);
