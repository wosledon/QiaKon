using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

internal sealed class QiaKonDatabaseInitializer
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<QiaKonDatabaseInitializer>? _logger;

    public QiaKonDatabaseInitializer(
        QiaKonAppDbContext dbContext,
        IConfiguration? configuration = null,
        ILogger<QiaKonDatabaseInitializer>? logger = null)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSchemaAsync(cancellationToken);

        var seeded = false;

        if (!await _dbContext.Departments.AnyAsync(cancellationToken))
        {
            _dbContext.Departments.AddRange(GetDepartmentSeeds());
            seeded = true;
        }

        if (!await _dbContext.Roles.AnyAsync(cancellationToken))
        {
            _dbContext.Roles.AddRange(GetRoleSeeds());
            seeded = true;
        }

        if (!await _dbContext.Users.AnyAsync(cancellationToken))
        {
            _dbContext.Users.AddRange(GetUserSeeds());
            seeded = true;
        }

        if (!await _dbContext.LlmProviders.AnyAsync(cancellationToken))
        {
            _dbContext.LlmProviders.AddRange(GetLlmProviderSeeds());
            seeded = true;
        }

        if (NormalizeProviderBaseUrls())
        {
            seeded = true;
        }

        if (!await _dbContext.LlmModels.AnyAsync(cancellationToken))
        {
            _dbContext.LlmModels.AddRange(GetLlmModelSeeds());
            seeded = true;
        }

        if (!await _dbContext.SystemConfigs.AnyAsync(cancellationToken))
        {
            _dbContext.SystemConfigs.Add(GetSystemConfigSeed());
            seeded = true;
        }

        if (!await _dbContext.Connectors.AnyAsync(cancellationToken))
        {
            _dbContext.Connectors.AddRange(GetConnectorSeeds());
            seeded = true;
        }

        if (!await _dbContext.AuditLogs.AnyAsync(cancellationToken))
        {
            _dbContext.AuditLogs.AddRange(GetAuditLogSeeds());
            seeded = true;
        }

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
        }

        _logger?.LogInformation(
            "QiaKon PostgreSQL initialized. Departments={DepartmentCount}, Users={UserCount}, Providers={ProviderCount}, Documents={DocumentCount}, Entities={EntityCount}, Relations={RelationCount}",
            await _dbContext.Departments.CountAsync(cancellationToken),
            await _dbContext.Users.CountAsync(cancellationToken),
            await _dbContext.LlmProviders.CountAsync(cancellationToken),
            await _dbContext.Documents.CountAsync(cancellationToken),
            await _dbContext.GraphEntities.CountAsync(cancellationToken),
            await _dbContext.GraphRelations.CountAsync(cancellationToken));
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        var sql = """
CREATE TABLE IF NOT EXISTS departments (
    "Id" uuid PRIMARY KEY,
    "Name" character varying(128) NOT NULL,
    "ParentId" uuid NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_departments_Name" ON departments ("Name");
CREATE INDEX IF NOT EXISTS "IX_departments_ParentId" ON departments ("ParentId");

CREATE TABLE IF NOT EXISTS roles (
    "Id" uuid PRIMARY KEY,
    "Name" character varying(128) NOT NULL,
    "Description" character varying(512) NOT NULL,
    "IsSystem" boolean NOT NULL,
    "PermissionsJson" text NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_roles_Name" ON roles ("Name");

CREATE TABLE IF NOT EXISTS users (
    "Id" uuid PRIMARY KEY,
    "Username" character varying(128) NOT NULL,
    "Email" character varying(256) NOT NULL,
    "PasswordHash" text NOT NULL,
    "DepartmentId" uuid NOT NULL,
    "Role" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "LastLoginAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_users_Username" ON users ("Username");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_users_Email" ON users ("Email");
CREATE INDEX IF NOT EXISTS "IX_users_DepartmentId" ON users ("DepartmentId");
CREATE INDEX IF NOT EXISTS "IX_users_Role" ON users ("Role");
CREATE INDEX IF NOT EXISTS "IX_users_IsActive" ON users ("IsActive");

CREATE TABLE IF NOT EXISTS llm_providers (
    "Id" uuid PRIMARY KEY,
    "Name" character varying(128) NOT NULL,
    "InterfaceType" integer NOT NULL,
    "BaseUrl" character varying(1024) NOT NULL,
    "ApiKey" text NULL,
    "TimeoutSeconds" integer NOT NULL,
    "RetryCount" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_llm_providers_Name" ON llm_providers ("Name");

CREATE TABLE IF NOT EXISTS llm_models (
    "Id" uuid PRIMARY KEY,
    "ProviderId" uuid NOT NULL,
    "Name" character varying(128) NOT NULL,
    "ActualModelName" character varying(256) NOT NULL,
    "ModelType" integer NOT NULL,
    "VectorDimensions" integer NULL,
    "MaxTokens" integer NULL,
    "IsEnabled" boolean NOT NULL,
    "IsDefault" boolean NOT NULL,
    "IsBuiltIn" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_llm_models_ProviderId_Name" ON llm_models ("ProviderId", "Name");
CREATE INDEX IF NOT EXISTS "IX_llm_models_ModelType_IsDefault" ON llm_models ("ModelType", "IsDefault");
CREATE INDEX IF NOT EXISTS "IX_llm_models_IsEnabled" ON llm_models ("IsEnabled");

CREATE TABLE IF NOT EXISTS system_configs (
    "Id" uuid PRIMARY KEY,
    "DefaultChunkingStrategy" character varying(128) NOT NULL,
    "ChunkSize" integer NOT NULL,
    "ChunkOverlap" integer NOT NULL,
    "DefaultVectorDimensions" integer NOT NULL,
    "CacheStrategy" character varying(128) NOT NULL,
    "CacheExpirationMinutes" integer NOT NULL,
    "PromptTemplate" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE IF NOT EXISTS connectors (
    "Id" uuid PRIMARY KEY,
    "Name" character varying(128) NOT NULL,
    "Type" integer NOT NULL,
    "State" integer NOT NULL,
    "BaseUrl" character varying(1024) NULL,
    "ConnectionString" text NULL,
    "EndpointsJson" text NULL,
    "LastHealthCheck" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_connectors_Name" ON connectors ("Name");
CREATE INDEX IF NOT EXISTS "IX_connectors_Type" ON connectors ("Type");
CREATE INDEX IF NOT EXISTS "IX_connectors_State" ON connectors ("State");

CREATE TABLE IF NOT EXISTS audit_logs (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "Username" character varying(128) NOT NULL,
    "Action" character varying(128) NOT NULL,
    "ResourceType" character varying(128) NOT NULL,
    "ResourceId" uuid NULL,
    "ResourceName" character varying(256) NULL,
    "Result" character varying(64) NOT NULL,
    "IpAddress" character varying(64) NULL,
    "Details" text NULL,
    "Timestamp" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_audit_logs_UserId" ON audit_logs ("UserId");
CREATE INDEX IF NOT EXISTS "IX_audit_logs_Action" ON audit_logs ("Action");
CREATE INDEX IF NOT EXISTS "IX_audit_logs_Timestamp" ON audit_logs ("Timestamp");

CREATE TABLE IF NOT EXISTS conversation_sessions (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NULL,
    "Title" character varying(300) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_conversation_sessions_UserId" ON conversation_sessions ("UserId");
CREATE INDEX IF NOT EXISTS "IX_conversation_sessions_CreatedAt" ON conversation_sessions ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_conversation_sessions_UpdatedAt" ON conversation_sessions ("UpdatedAt");

CREATE TABLE IF NOT EXISTS conversation_messages (
    "Id" uuid PRIMARY KEY,
    "ConversationId" uuid NOT NULL,
    "Role" character varying(32) NOT NULL,
    "Content" text NOT NULL,
    "SourcesJson" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_conversation_messages_ConversationId_CreatedAt" ON conversation_messages ("ConversationId", "CreatedAt");
""";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static IReadOnlyList<DepartmentRow> GetDepartmentSeeds()
        => QiaKonSeedData.GetDepartments()
            .Select(seed => new DepartmentRow
            {
                Id = seed.Id,
                Name = seed.Name,
                ParentId = seed.ParentId,
                CreatedAt = seed.CreatedAt
            })
            .ToList();

    private static IReadOnlyList<RoleRow> GetRoleSeeds()
        =>
        [
            new()
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = nameof(UserRole.Admin),
                Description = "系统管理员，拥有所有权限",
                IsSystem = true,
                PermissionsJson = PostgresPersistenceJson.Serialize(PostgresPlatformDefaults.AdminPermissions())
            },
            new()
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Name = nameof(UserRole.KnowledgeAdmin),
                Description = "知识管理员，管理文档和图谱",
                IsSystem = true,
                PermissionsJson = PostgresPersistenceJson.Serialize(PostgresPlatformDefaults.KnowledgeAdminPermissions())
            },
            new()
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = nameof(UserRole.DepartmentManager),
                Description = "部门经理，管理本部门资源",
                IsSystem = true,
                PermissionsJson = PostgresPersistenceJson.Serialize(PostgresPlatformDefaults.DepartmentManagerPermissions())
            },
            new()
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Name = nameof(UserRole.DepartmentMember),
                Description = "部门成员，访问本部门资源",
                IsSystem = true,
                PermissionsJson = PostgresPersistenceJson.Serialize(PostgresPlatformDefaults.DepartmentMemberPermissions())
            },
            new()
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Name = nameof(UserRole.Guest),
                Description = "访客，仅访问公开资源",
                IsSystem = true,
                PermissionsJson = PostgresPersistenceJson.Serialize(PostgresPlatformDefaults.GuestPermissions())
            }
        ];

    private static IReadOnlyList<UserRow> GetUserSeeds()
        =>
        [
            new() { Id = QiaKonSeedData.AdminUserId, Username = "admin", Email = "admin@qiakon.com", PasswordHash = "password123", DepartmentId = QiaKonSeedData.HrDepartmentId, Role = UserRole.Admin, IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddYears(-1) },
            new() { Id = QiaKonSeedData.KnowledgeAdminUserId, Username = "kb_admin", Email = "kb_admin@qiakon.com", PasswordHash = "password123", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, Role = UserRole.KnowledgeAdmin, IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-2), CreatedAt = DateTime.UtcNow.AddMonths(-10) },
            new() { Id = QiaKonSeedData.DepartmentManagerUserId, Username = "dept_manager", Email = "dept_mgr@qiakon.com", PasswordHash = "password123", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, Role = UserRole.DepartmentManager, IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-3), CreatedAt = DateTime.UtcNow.AddMonths(-8) },
            new() { Id = QiaKonSeedData.EngineerUserId, Username = "engineer", Email = "engineer@qiakon.com", PasswordHash = "password123", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, Role = UserRole.DepartmentMember, IsActive = true, LastLoginAt = DateTime.UtcNow.AddHours(-12), CreatedAt = DateTime.UtcNow.AddMonths(-6) },
            new() { Id = QiaKonSeedData.SalesUserId, Username = "guest", Email = "guest@qiakon.com", PasswordHash = "password123", DepartmentId = QiaKonSeedData.SalesDepartmentId, Role = UserRole.Guest, IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-5), CreatedAt = DateTime.UtcNow.AddMonths(-5) },
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111110"), Username = "zhangwei", Email = "zhangwei@qiakon.com", PasswordHash = "password123", DepartmentId = QiaKonSeedData.EngineeringDepartmentId, Role = UserRole.DepartmentMember, IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-4), CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Username = "lina", Email = "lina@qiakon.com", PasswordHash = "password123", DepartmentId = QiaKonSeedData.SalesDepartmentId, Role = UserRole.DepartmentManager, IsActive = true, LastLoginAt = DateTime.UtcNow.AddDays(-6), CreatedAt = DateTime.UtcNow.AddMonths(-4) }
        ];

    private static IReadOnlyList<LlmProviderRow> GetLlmProviderSeeds()
        =>
        [
            new() { Id = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Qwen 云服务", InterfaceType = LlmInterfaceType.OpenAI, BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", ApiKey = "sk-qwen-xxxxx", TimeoutSeconds = 60, RetryCount = 3, CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "OpenAI 服务", InterfaceType = LlmInterfaceType.OpenAI, BaseUrl = "https://api.openai.com/v1", ApiKey = "sk-openai-xxxxx", TimeoutSeconds = 60, RetryCount = 3, CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("11111111-0000-0000-0000-000000000003"), Name = "Anthropic 服务", InterfaceType = LlmInterfaceType.Anthropic, BaseUrl = "https://api.anthropic.com", ApiKey = "sk-ant-xxxxx", TimeoutSeconds = 60, RetryCount = 3, CreatedAt = DateTime.UtcNow.AddMonths(-3) }
        ];

    private bool NormalizeProviderBaseUrls()
    {
        var changed = false;

        foreach (var provider in _dbContext.LlmProviders)
        {
            var normalizedBaseUrl = LlmProviderUrlNormalizer.NormalizeBaseUrl(provider.BaseUrl, provider.InterfaceType);
            if (!string.Equals(provider.BaseUrl, normalizedBaseUrl, StringComparison.Ordinal))
            {
                provider.BaseUrl = normalizedBaseUrl;
                changed = true;
            }
        }

        return changed;
    }

    private static IReadOnlyList<LlmModelRow> GetLlmModelSeeds()
        =>
        [
            new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000001"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Qwen-Max-推理", ActualModelName = "qwen-max", ModelType = LlmModelType.Inference, VectorDimensions = null, MaxTokens = 128000, IsEnabled = true, IsDefault = true, IsBuiltIn = false, CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000002"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Qwen-Turbo-推理", ActualModelName = "qwen-turbo", ModelType = LlmModelType.Inference, VectorDimensions = null, MaxTokens = 128000, IsEnabled = true, IsDefault = false, IsBuiltIn = false, CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000003"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Qwen-Embed-分块", ActualModelName = "text-embedding-3-small", ModelType = LlmModelType.Embedding, VectorDimensions = 1536, MaxTokens = null, IsEnabled = true, IsDefault = true, IsBuiltIn = false, CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000004"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "GPT-4o-推理", ActualModelName = "gpt-4o", ModelType = LlmModelType.Inference, VectorDimensions = null, MaxTokens = 128000, IsEnabled = true, IsDefault = false, IsBuiltIn = false, CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000005"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "GPT-4o-Mini-推理", ActualModelName = "gpt-4o-mini", ModelType = LlmModelType.Inference, VectorDimensions = null, MaxTokens = 128000, IsEnabled = true, IsDefault = false, IsBuiltIn = false, CreatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000006"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000003"), Name = "Claude-3.5-Sonnet-推理", ActualModelName = "claude-3-5-sonnet-20241022", ModelType = LlmModelType.Inference, VectorDimensions = null, MaxTokens = 200000, IsEnabled = true, IsDefault = false, IsBuiltIn = false, CreatedAt = DateTime.UtcNow.AddMonths(-3) },
            new() { Id = Guid.Parse("22222222-ffff-ffff-ffff-fffffffffff1"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "bge-large-zh", ActualModelName = "bge-large-zh-v1.5", ModelType = LlmModelType.Embedding, VectorDimensions = 1024, MaxTokens = null, IsEnabled = true, IsDefault = false, IsBuiltIn = true, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
            new() { Id = Guid.Parse("22222222-ffff-ffff-ffff-fffffffffff2"), ProviderId = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "bge-base-zh", ActualModelName = "bge-base-zh-v1.5", ModelType = LlmModelType.Embedding, VectorDimensions = 768, MaxTokens = null, IsEnabled = true, IsDefault = false, IsBuiltIn = true, CreatedAt = DateTime.UtcNow.AddMonths(-2) }
        ];

    private static SystemConfigRow GetSystemConfigSeed()
    {
        var defaults = PostgresPlatformDefaults.DefaultSystemConfig();
        return new SystemConfigRow
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            DefaultChunkingStrategy = defaults.DefaultChunkingStrategy,
            ChunkSize = defaults.ChunkSize,
            ChunkOverlap = defaults.ChunkOverlap,
            DefaultVectorDimensions = defaults.DefaultVectorDimensions,
            CacheStrategy = defaults.CacheStrategy,
            CacheExpirationMinutes = defaults.CacheExpirationMinutes,
            PromptTemplate = defaults.PromptTemplate,
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            UpdatedAt = DateTime.UtcNow.AddMonths(-3)
        };
    }

    private IReadOnlyList<ConnectorRow> GetConnectorSeeds()
    {
        var defaultConnectionString = _configuration?.GetConnectionString("Default") ?? "Host=127.0.0.1;Port=5432;Database=QiaKon;Username=admin;Password=admin@123";
        var redisConnectionString = _configuration?.GetConnectionString("Redis") ?? "127.0.0.1:6379,password=admin@123";
        return
        [
            new() { Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Name = "PostgreSQL (生产)", Type = ConnectorType.Npgsql, State = ConnectorState.Connected, BaseUrl = null, ConnectionString = defaultConnectionString, EndpointsJson = null, LastHealthCheck = DateTime.UtcNow, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
            new() { Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Name = "Redis (生产)", Type = ConnectorType.Redis, State = ConnectorState.Connected, BaseUrl = null, ConnectionString = redisConnectionString, EndpointsJson = null, LastHealthCheck = DateTime.UtcNow, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
            new() { Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), Name = "Kafka (生产)", Type = ConnectorType.MessageQueue, State = ConnectorState.Connected, BaseUrl = null, ConnectionString = "BootstrapServers=127.0.0.1:9092", EndpointsJson = null, LastHealthCheck = DateTime.UtcNow, CreatedAt = DateTime.UtcNow.AddMonths(-2) }
        ];
    }

    private static IReadOnlyList<AuditLogRow> GetAuditLogSeeds()
        =>
        [
            new() { Id = Guid.NewGuid(), UserId = QiaKonSeedData.AdminUserId, Username = "admin", Action = "登录", ResourceType = "Auth", ResourceId = null, ResourceName = null, Result = "成功", IpAddress = "127.0.0.1", Details = "登录系统", Timestamp = DateTime.UtcNow.AddHours(-48) },
            new() { Id = Guid.NewGuid(), UserId = QiaKonSeedData.EngineerUserId, Username = "engineer", Action = "登录", ResourceType = "Auth", ResourceId = null, ResourceName = null, Result = "成功", IpAddress = "192.168.1.100", Details = "登录系统", Timestamp = DateTime.UtcNow.AddHours(-24) },
            new() { Id = Guid.NewGuid(), UserId = QiaKonSeedData.AdminUserId, Username = "admin", Action = "创建", ResourceType = "Document", ResourceId = Guid.Parse("d1111111-1111-1111-1111-111111111111"), ResourceName = "QiaKon平台架构设计文档", Result = "成功", IpAddress = "127.0.0.1", Details = "上传新文档", Timestamp = DateTime.UtcNow.AddHours(-20) },
            new() { Id = Guid.NewGuid(), UserId = QiaKonSeedData.EngineerUserId, Username = "engineer", Action = "更新", ResourceType = "Document", ResourceId = Guid.Parse("d6666666-6666-6666-6666-666666666666"), ResourceName = "研发部项目管理制度", Result = "成功", IpAddress = "192.168.1.100", Details = "修改文档内容", Timestamp = DateTime.UtcNow.AddHours(-12) },
            new() { Id = Guid.NewGuid(), UserId = QiaKonSeedData.AdminUserId, Username = "admin", Action = "创建", ResourceType = "GraphEntity", ResourceId = null, ResourceName = "QiaKon平台", Result = "成功", IpAddress = "127.0.0.1", Details = "创建图谱实体", Timestamp = DateTime.UtcNow.AddHours(-6) },
            new() { Id = Guid.NewGuid(), UserId = QiaKonSeedData.EngineerUserId, Username = "engineer", Action = "问答", ResourceType = "Retrieval", ResourceId = null, ResourceName = null, Result = "成功", IpAddress = "192.168.1.100", Details = "RAG问答: QiaKon是什么", Timestamp = DateTime.UtcNow.AddHours(-2) }
        ];

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
