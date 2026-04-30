# QiaKon KAG 平台功能规格说明书

> **文档类型**：功能规格说明书（FSD）  
> **版本**：v1.0  
> **日期**：2026-04-28  
> **状态**：初稿

---

## 一、模块总览

| 模块                                | 项目            | 负责人 | 状态   |
| ----------------------------------- | --------------- | ------ | ------ |
| QiaKon.Api                          | API 层          | -      | 待开发 |
| QiaKon.Contracts                    | 通用契约        | -      | 待开发 |
| QiaKon.Cache                        | 缓存抽象        | -      | 待开发 |
| QiaKon.Cache.Memory                 | 内存缓存        | -      | 待开发 |
| QiaKon.Cache.Hybrid                 | 混合缓存        | -      | 待开发 |
| QiaKon.Cache.Redis                  | Redis 缓存      | -      | 待开发 |
| QiaKon.Connector                    | 连接器抽象      | -      | 待开发 |
| QiaKon.Connector.Http               | HTTP 连接器     | -      | 待开发 |
| QiaKon.Connector.Npgsql             | Npgsql 连接器   | -      | 待开发 |
| QiaKon.Llm                          | LLM 核心        | -      | 待开发 |
| QiaKon.Llm.Context                  | 上下文管理      | -      | 待开发 |
| QiaKon.Llm.Prompt                   | Prompt 管理     | -      | 待开发 |
| QiaKon.Llm.Providers                | LLM 提供商      | -      | 待开发 |
| QiaKon.Llm.Tokenization             | Token 计算      | -      | 待开发 |
| QiaKon.Workflow                     | 工作流引擎      | -      | 待开发 |
| QiaKon.Retrieval                    | 检索管道        | -      | 待开发 |
| QiaKon.Retrieval.Chunking           | 文档分块        | -      | 待开发 |
| QiaKon.Retrieval.Embedding          | 向量嵌入        | -      | 待开发 |
| QiaKon.Retrieval.VectorStore        | 向量存储        | -      | 待开发 |
| QiaKon.Retrieval.VectorStore.Npgsql | Npgsql 向量存储 | -      | 待开发 |
| QiaKon.Graph.Engine                 | 图谱引擎        | -      | 待开发 |
| QiaKon.Graph.Engine.Npgsql          | Npgsql 图谱存储 | -      | 待开发 |
| QiaKon.Queue                        | 消息队列抽象    | -      | 待开发 |
| QiaKon.Queue.Kafka                  | Kafka 实现      | -      | 待开发 |
| QiaKon.EntityFrameworkCore          | EF Core 集成    | -      | 待开发 |

---

## 二、公共模块

### 2.1 QiaKon.Contracts

#### 2.1.1 实体基类

```csharp
// IEntity - 基础实体接口
public interface IEntity
{
    Guid Id { get; set; }
}

// IAuditable - 审计接口
public interface IAuditable
{
    Guid CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    Guid? ModifiedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
}

// ISoftDelete - 软删除接口
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

// IVersionable - 并发版本接口
public interface IVersionable
{
    byte[] RowVersion { get; set; }
}
```

#### 2.1.2 基类实现

| 类名                   | 继承关系                         | 说明                     |
| ---------------------- | -------------------------------- | ------------------------ |
| `EntityBase`           | IEntity                          | 实现 Id 属性，默认 newId |
| `AuditableEntityBase`  | EntityBase, IAuditable           | 增加审计字段             |
| `SoftDeleteEntityBase` | AuditableEntityBase, ISoftDelete | 增加软删除字段           |

---

### 2.2 QiaKon.Cache

#### 2.2.1 缓存接口

```csharp
public interface ICache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public class CacheEntryOptions
{
    public TimeSpan? AbsoluteExpiration { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public DateTimeOffset? AbsoluteExpirationRelativeToNow { get; set; }
}
```

#### 2.2.2 缓存实现

| 实现          | 说明                           | 优先级 |
| ------------- | ------------------------------ | ------ |
| `MemoryCache` | L1 进程内缓存                  | 最快   |
| `HybridCache` | L2 混合缓存（Memory + 分布式） | 中     |
| `RedisCache`  | L3 分布式缓存                  | 最慢   |

#### 2.2.3 缓存策略

| 策略          | 实现        | 说明                         |
| ------------- | ----------- | ---------------------------- |
| Cache-Aside   | 应用层      | 应用自行管理缓存更新（默认） |
| Read-Through  | HybridCache | 缓存未命中自动从源加载       |
| Write-Through | HybridCache | 写入时同步更新缓存           |
| Write-Behind  | RedisCache  | 异步写入，提升性能           |

---

### 2.3 QiaKon.Connector

#### 2.3.1 连接器接口

```csharp
public interface IConnector : IDisposable, IAsyncDisposable
{
    string Name { get; }
    ConnectorType Type { get; }
    ConnectorState State { get; }
    
    Task InitializeAsync(CancellationToken ct = default);
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);
}

public enum ConnectorType { Http, Npgsql, Kafka }
public enum ConnectorState { Disconnected, Connecting, Connected, Healthy, Unhealthy, Closed }
```

#### 2.3.2 HTTP 连接器

```csharp
public interface IHttpConnector : IConnector
{
    Task<HttpResponse> CallAsync(string endpointName, IDictionary<string, object>? parameters = null, CancellationToken ct = default);
}
```

**配置项**：

| 配置项      | 类型          | 说明                          |
| ----------- | ------------- | ----------------------------- |
| BaseUrl     | string        | API 基础地址                  |
| Endpoints   | Dictionary    | 端点名称 → URL/Method/Mapping |
| Timeout     | TimeSpan      | 请求超时（默认 30s）          |
| RetryPolicy | RetryStrategy | 重试策略                      |

#### 2.3.3 Npgsql 连接器

```csharp
public interface INpgsqlConnector : IConnector
{
    Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct = default);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default);
}
```

**配置项**：

| 配置项           | 类型     | 说明                  |
| ---------------- | -------- | --------------------- |
| ConnectionString | string   | PostgreSQL 连接字符串 |
| CommandTimeout   | TimeSpan | 命令超时（默认 30s）  |

---

## 三、LLM 模块

### 3.1 QiaKon.Llm

#### 3.1.1 核心接口

```csharp
public interface ILlmClient
{
    string Model { get; }
    
    Task<ChatCompletion> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);
        
    IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);
}

public class ChatCompletionRequest
{
    public required string Model { get; set; }
    public required IList<ChatMessage> Messages { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string? ResponseFormat { get; set; }
    public IReadOnlyList<string>? Stop { get; set; }
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
}
```

#### 3.1.2 消息类型

```csharp
public sealed record ChatMessage
{
    public required MessageRole Role { get; init; }
    public required string Content { get; init; }
    public string? Name { get; init; }
}

public enum MessageRole { System, User, Assistant, Tool }
```

#### 3.1.3 重试策略

```csharp
public record LlmRetryStrategy
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public BackoffType Backoff { get; init; } = BackoffType.Exponential;
}

public enum BackoffType { Fixed, Linear, Exponential }
```

---

### 3.2 QiaKon.Llm.Context

#### 3.2.1 对话上下文

```csharp
public sealed class ConversationContext
{
    public ConversationContext(
        int? maxMessages = null,
        int? maxTokens = null,
        IMessageTrimmer? trimmer = null);
        
    public int Count { get; }
    
    public void AddMessage(ChatMessage message);
    public void AddMessages(IEnumerable<ChatMessage> messages);
    public IReadOnlyList<ChatMessage> GetMessages();
    public void Clear();
    public ChatMessage? RemoveLast();
    public void SetSystemPrompt(string systemPrompt);
    public int EstimateTokenCount(Func<string, int>? tokenCounter = null);
}
```

#### 3.2.2 上下文模板

```csharp
public sealed record ContextTemplate
{
    public required string Name { get; init; }
    public string? SystemPromptTemplate { get; init; }
    public IReadOnlyList<ChatMessage>? InitialMessages { get; init; }
    public int? MaxMessages { get; init; }
    public int? MaxTokens { get; init; }
    
    public ConversationContext CreateContext(
        IDictionary<string, string>? variables = null,
        IMessageTrimmer? trimmer = null);
}
```

#### 3.2.3 消息裁剪器

```csharp
public interface IMessageTrimmer
{
    (IList<ChatMessage> kept, IList<ChatMessage> removed) Trim(
        IList<ChatMessage> messages,
        int maxMessages,
        int maxTokens,
        Func<string, int> tokenCounter);
}

public class DefaultMessageTrimmer : IMessageTrimmer { }
public class PriorityMessageTrimmer : IMessageTrimmer { }
public class SummaryMessageTrimmer : IMessageTrimmer { }
```

---

### 3.3 QiaKon.Llm.Agent

#### 3.3.1 Agent 接口

```csharp
public interface ILlmAgent
{
    string Name { get; }
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default);
}

public sealed class AgentRequest
{
    public required string UserInput { get; init; }
    public IReadOnlyList<ChatMessage>? Messages { get; init; }
    public Action<ChatMessage>? OnMessageAdded { get; init; }
    public IDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();
    public LlmInferenceOptions? InferenceOptions { get; init; }
    public IReadOnlyList<LlmTool>? Tools { get; init; }
    public int MaxTurns { get; init; } = 10;
}

public sealed class AgentResponse
{
    public required string Response { get; init; }
    public bool IsComplete { get; init; }
    public int Turns { get; init; }
    public IReadOnlyList<ToolExecutionResult> ToolResults { get; init; } = [];
    public string? Error { get; init; }
}
```

#### 3.3.2 工具定义

```csharp
public sealed class LlmTool
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ParametersJsonSchema { get; init; }
}

public sealed record ToolExecutionResult
{
    public required string ToolName { get; init; }
    public required string Parameters { get; init; }
    public required string Result { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}
```

#### 3.3.3 Agent 编排

```csharp
// Agent 链（串联）
public interface IAgentChain
{
    string Name { get; }
    IReadOnlyList<ILlmAgent> Agents { get; }
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default);
}

// Agent 池（并联）
public interface IAgentPool
{
    string Name { get; }
    IReadOnlyList<ILlmAgent> Agents { get; }
    Task<IReadOnlyList<AgentResponse>> ExecuteAllAsync(AgentRequest request, CancellationToken ct = default);
}

// 路由 Agent
public interface IRouterAgent : ILlmAgent
{
    void RegisterRoute(string intent, ILlmAgent agent);
    void RegisterRoute(Func<string, string?> intentClassifier, ILlmAgent agent);
}
```

---

## 四、工作流模块

### 4.1 QiaKon.Workflow

#### 4.1.1 核心接口

```csharp
public interface IPipeline
{
    string Name { get; }
    Task<PipelineResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

public interface IStep
{
    string Name { get; }
    Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

public interface IStage
{
    string Name { get; }
    StageMode Mode { get; }
    IReadOnlyList<IStep> Steps { get; }
    Task<StageResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

public enum StageMode { Sequential, Parallel }
```

#### 4.1.2 上下文

```csharp
public sealed class WorkflowContext
{
    public string? PipelineName { get; set; }
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
    
    public void SetItem(string key, object value);
    public T? GetItem<T>(string key);
    public bool TryGetItem<T>(string key, out T? value);
}
```

#### 4.1.3 步骤基类

```csharp
public abstract class StepBase : IStep
{
    public required string Name { get; init; }
    protected virtual Task<StepResult> OnExecuteAsync(WorkflowContext context, CancellationToken ct) 
        => Task.FromResult(StepResult.Success());
        
    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default)
    {
        return await OnExecuteAsync(context, ct);
    }
}

// 重试步骤封装
public class RetryStep : StepBase
{
    public required IStep InnerStep { get; init; }
    public int MaxRetries { get; init; } = 3;
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(1);
    public BackoffType Backoff { get; init; } = BackoffType.Exponential;
}
```

#### 4.1.4 分支步骤

```csharp
public class BranchingSteps : StepBase
{
    public required Func<WorkflowContext, string> BranchSelector { get; init; }
    public required IDictionary<string, IStep> Branches { get; init; }
    public IStep? DefaultBranch { get; init; }
}
```

---

## 五、检索模块

### 5.1 QiaKon.Retrieval

#### 5.1.1 文档接口

```csharp
public interface IDocument
{
    Guid Id { get; }
    string Title { get; }
    string Content { get; }
    IReadOnlyDictionary<string, object>? Metadata { get; }
}

public interface IChunk
{
    Guid Id { get; }
    Guid DocumentId { get; }
    string Content { get; }
    int Order { get; }
    ReadOnlyMemory<float> Embedding { get; }
}
```

#### 5.1.2 分块策略

```csharp
public interface IChunkingStrategy
{
    string Name { get; }
    IAsyncEnumerable<IChunk> ChunkAsync(IDocument document, CancellationToken ct = default);
}

// 内置分块策略
public class SemanticChunkingStrategy : IChunkingStrategy { }
public class RecursiveChunkingStrategy : IChunkingStrategy { }
public class FixedSizeChunkingStrategy : IChunkingStrategy { }
public class TableChunkingStrategy : IChunkingStrategy { }

// MoE 分块策略
public class MoEChunkingStrategy : IChunkingStrategy
{
    public IReadOnlyList<IChunkingStrategy> Experts { get; }
    public Func<IDocument, IChunkingStrategy> Router { get; }
}
```

#### 5.1.3 RAG 管道

```csharp
public interface IRagPipeline
{
    Task<RagDocumentRecord> IndexAsync(IDocument document, CancellationToken ct = default);
    Task<RagDocumentRecord> IndexAsync(IDocument document, bool skipProcessing, CancellationToken ct = default);
    
    Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        string query,
        RetrievalOptions? options = null,
        CancellationToken ct = default);
        
    Task<bool> DeleteAsync(Guid documentId, CancellationToken ct = default);
}

public class RetrievalOptions
{
    public int TopK { get; set; } = 10;
    public float? ScoreThreshold { get; set; }
    public IDictionary<string, object>? Filters { get; set; }
    public bool HybridSearch { get; set; } = false;
    public bool EnableRerank { get; set; } = false;
}

public sealed record RagDocumentRecord
{
    public required Guid DocumentId { get; init; }
    public required string Title { get; init; }
    public required int ChunkCount { get; init; }
    public required DateTimeOffset IndexedAt { get; init; }
    public required IReadOnlyList<Guid> ChunkIds { get; init; }
}

public sealed record RagSearchResult
{
    public required IChunk Chunk { get; init; }
    public required float Score { get; init; }
    public IDocument? Document { get; init; }
}
```

#### 5.1.4 向量存储接口

```csharp
public interface IVectorStore
{
    Task<Guid> UpsertAsync(IChunk chunk, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> UpsertBatchAsync(IEnumerable<IChunk> chunks, CancellationToken ct = default);
    
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        VectorSearchOptions? options = null,
        CancellationToken ct = default);
        
    Task<bool> DeleteAsync(Guid chunkId, CancellationToken ct = default);
    Task<bool> DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
}

public class VectorSearchOptions
{
    public int TopK { get; set; } = 10;
    public float? ScoreThreshold { get; set; }
    public IDictionary<string, object>? Filters { get; set; }
}

public record VectorSearchResult
{
    public required Guid ChunkId { get; init; }
    public required float Score { get; init; }
}
```

---

### 5.2 QiaKon.Retrieval.VectorStore.Npgsql

#### 5.2.1 表结构

```sql
-- 文档表
CREATE TABLE documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(500) NOT NULL,
    content TEXT NOT NULL,
    metadata JSONB,
    department_id UUID,
    is_public BOOLEAN DEFAULT false,
    access_level VARCHAR(50) DEFAULT 'Department',
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    modified_by UUID,
    modified_at TIMESTAMPTZ,
    version INTEGER NOT NULL DEFAULT 1,
    is_deleted BOOLEAN DEFAULT false
);

-- 文档块表
CREATE TABLE document_chunks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID NOT NULL REFERENCES documents(id),
    content TEXT NOT NULL,
    "order" INTEGER NOT NULL,
    embedding VECTOR(1536),  -- pgvector
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 向量索引
CREATE INDEX idx_chunks_embedding ON document_chunks 
    USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- 权限过滤索引
CREATE INDEX idx_documents_department ON documents(department_id) WHERE is_deleted = false;
CREATE INDEX idx_documents_access ON documents(is_public, access_level) WHERE is_deleted = false;
```

#### 5.2.2 相似度搜索 SQL

```sql
-- 向量相似度搜索（带权限过滤）
SELECT 
    c.id, c.content, c.document_id,
    1 - (c.embedding <=> @query_embedding) AS score,
    d.title, d.department_id, d.is_public, d.access_level
FROM document_chunks c
JOIN documents d ON c.document_id = d.id
WHERE 
    c.is_deleted = false 
    AND d.is_deleted = false
    AND (
        -- 公开文档
        d.is_public = true
        -- 同一部门
        OR d.department_id = ANY(@user_department_ids)
        -- 管理员
        OR @user_role = 'Admin'
    )
    AND (@score_threshold IS NULL OR 1 - (c.embedding <=> @query_embedding) >= @score_threshold)
ORDER BY c.embedding <=> @query_embedding
LIMIT @top_k;
```

---

## 六、知识图谱模块

### 6.1 QiaKon.Graph.Engine

#### 6.1.1 图接口

```csharp
public interface IGraphStore
{
    // 实体操作
    Task<Entity> CreateEntityAsync(Entity entity, CancellationToken ct = default);
    Task<Entity?> GetEntityAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Entity>> QueryEntitiesAsync(EntityQuery query, CancellationToken ct = default);
    Task<bool> UpdateEntityAsync(Entity entity, CancellationToken ct = default);
    Task<bool> DeleteEntityAsync(Guid id, CancellationToken ct = default);
    
    // 关系操作
    Task<Relation> CreateRelationAsync(Relation relation, CancellationToken ct = default);
    Task<Relation?> GetRelationAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Relation>> QueryRelationsAsync(RelationQuery query, CancellationToken ct = default);
    Task<bool> DeleteRelationAsync(Guid id, CancellationToken ct = default);
    
    // 图查询
    Task<IReadOnlyList<GraphPath>> QueryPathsAsync(PathQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<Entity>> ExecuteCypherAsync(string cypher, CancellationToken ct = default);
}

public class Entity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsPublic { get; set; }
    public string? AccessLevel { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class Relation
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    public required string Type { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsPublic { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}
```

#### 6.1.2 图查询

```csharp
public class PathQuery
{
    public Guid? StartEntityId { get; set; }
    public Guid? EndEntityId { get; set; }
    public string? RelationType { get; set; }
    public int MaxHops { get; set; } = 3;
}

public class EntityQuery
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool? IsPublic { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}
```

---

### 6.2 QiaKon.Graph.Engine.Npgsql

#### 6.2.1 表结构

```sql
-- 实体表
CREATE TABLE graph_entities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(500) NOT NULL,
    type VARCHAR(100) NOT NULL,
    properties JSONB NOT NULL DEFAULT '{}',
    department_id UUID,
    is_public BOOLEAN DEFAULT false,
    access_level VARCHAR(50) DEFAULT 'Department',
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    modified_by UUID,
    modified_at TIMESTAMPTZ,
    is_deleted BOOLEAN DEFAULT false
);

-- 关系表
CREATE TABLE graph_relations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id UUID NOT NULL REFERENCES graph_entities(id),
    target_id UUID NOT NULL REFERENCES graph_entities(id),
    relation_type VARCHAR(100) NOT NULL,
    properties JSONB NOT NULL DEFAULT '{}',
    department_id UUID,
    is_public BOOLEAN DEFAULT false,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN DEFAULT false
);

-- 索引
CREATE INDEX idx_entities_type ON graph_entities(type) WHERE is_deleted = false;
CREATE INDEX idx_entities_name ON graph_entities USING gin(to_tsvector('simple', name)) WHERE is_deleted = false;
CREATE INDEX idx_relations_source ON graph_relations(source_id) WHERE is_deleted = false;
CREATE INDEX idx_relations_target ON graph_relations(target_id) WHERE is_deleted = false;
CREATE INDEX idx_relations_type ON graph_relations(relation_type) WHERE is_deleted = false;
```

---

## 七、权限模块

### 7.1 权限判断服务

#### 7.1.1 接口定义

```csharp
public interface IPermissionService
{
    Task<PermissionResult> CheckAccessAsync(
        Guid userId,
        ResourceType resourceType,
        Guid resourceId,
        PermissionOperation operation,
        CancellationToken ct = default);
        
    Task<IReadOnlyList<Guid>> GetAccessibleDepartmentsAsync(
        Guid userId,
        CancellationToken ct = default);
        
    Task<string?> GetUserRoleAsync(Guid userId, CancellationToken ct = default);
}

public enum ResourceType { Document, Chunk, Entity, Relation }
public enum PermissionOperation { Read, Write, Delete }

public record PermissionResult
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
}
```

#### 7.1.2 权限判断逻辑

```csharp
// 权限判断伪代码
async Task<PermissionResult> CheckAccessAsync(userId, resourceType, resourceId, operation)
{
    var user = await userService.GetUserAsync(userId);
    var resource = await resourceService.GetAsync(resourceType, resourceId);
    
    // Admin 拥有所有权限
    if (user.Role == "Admin") return Allowed();
    
    // 公开资源（Public）所有登录用户可读
    if (resource.IsPublic && operation == Read) return Allowed();
    
    // 同一部门
    if (resource.DepartmentId == user.DepartmentId)
    {
        if (operation == Read) return Allowed();
        if (operation == Write && user.Role is "DepartmentManager" or "KnowledgeAdmin") return Allowed();
        if (operation == Delete && user.Role == "DepartmentManager") return Allowed();
    }
    
    // AccessLevel 为 Confidential 仅 Admin 和本部门 Manager 可写
    if (resource.AccessLevel == "Confidential" && operation == Write)
    {
        if (user.Role == "Admin") return Allowed();
        if (user.Role == "DepartmentManager" && resource.DepartmentId == user.DepartmentId) return Allowed();
        return Denied("Confidential 资源仅管理员和部门经理可写");
    }
    
    return Denied();
}
```

---

## 八、API 接口规范

### 8.1 文档管理

#### 8.1.1 上传文档

```
POST /api/documents
Content-Type: multipart/form-data

Request:
- file: binary (PDF/Word/Markdown/TXT)
- title: string (可选，从文件名提取)
- departmentId: uuid (可选，默认当前用户部门)
- isPublic: boolean (默认 false)
- accessLevel: string (Public/Department/Restricted/Confidential)

Response 201:
{
    "id": "uuid",
    "title": "string",
    "status": "Indexing",
    "createdAt": "datetime"
}
```

#### 8.1.2 获取文档列表

```
GET /api/documents?page=1&pageSize=20&departmentId=uuid

Response 200:
{
    "items": [
        {
            "id": "uuid",
            "title": "string",
            "departmentId": "uuid",
            "isPublic": false,
            "accessLevel": "Department",
            "createdAt": "datetime",
            "version": 1
        }
    ],
    "totalCount": 100,
    "page": 1,
    "pageSize": 20
}
```

#### 8.1.3 获取文档详情

```
GET /api/documents/{id}

Response 200:
{
    "id": "uuid",
    "title": "string",
    "content": "string",
    "metadata": {},
    "departmentId": "uuid",
    "isPublic": false,
    "accessLevel": "Department",
    "createdBy": "uuid",
    "createdAt": "datetime",
    "version": 1
}
```

#### 8.1.4 更新文档

```
PUT /api/documents/{id}
Content-Type: application/json

Request:
{
    "title": "string",
    "content": "string",
    "isPublic": false,
    "accessLevel": "Department"
}

Response 200:
{
    "id": "uuid",
    "status": "Indexing",
    "version": 2
}
```

#### 8.1.5 删除文档

```
DELETE /api/documents/{id}

Response 204: No Content
```

#### 8.1.6 全量重建索引

```
POST /api/documents/reindex

Response 202:
{
    "jobId": "uuid",
    "status": "Processing"
}
```

---

### 8.2 检索 API

#### 8.2.1 向量检索

```
POST /api/retrieve
Content-Type: application/json

Request:
{
    "query": "string",
    "topK": 10,
    "scoreThreshold": 0.7,
    "filters": {
        "departmentId": "uuid",
        "accessLevel": "Department"
    },
    "hybridSearch": false,
    "enableRerank": false
}

Response 200:
{
    "results": [
        {
            "chunkId": "uuid",
            "documentId": "uuid",
            "content": "string",
            "score": 0.95,
            "document": {
                "id": "uuid",
                "title": "string"
            }
        }
    ],
    "queryTime": 50
}
```

#### 8.2.2 RAG 对话

```
POST /api/rag/chat
Content-Type: application/json

Request:
{
    "query": "string",
    "conversationId": "uuid",
    "contextTemplate": "qa-assistant",
    "variables": {
        "domain": "技术"
    },
    "retrievalOptions": {
        "topK": 5,
        "scoreThreshold": 0.8
    }
}

Response 200:
{
    "response": "string",
    "sources": [
        {
            "chunkId": "uuid",
            "content": "string",
            "score": 0.95,
            "documentTitle": "string"
        }
    ],
    "conversationId": "uuid",
    "turns": 1
}
```

---

### 8.3 知识图谱 API

#### 8.3.1 创建实体

```
POST /api/graph/entities
Content-Type: application/json

Request:
{
    "name": "string",
    "type": "string",
    "properties": {},
    "departmentId": "uuid",
    "isPublic": false,
    "accessLevel": "Department"
}

Response 201:
{
    "id": "uuid",
    "name": "string",
    "type": "string",
    "createdAt": "datetime"
}
```

#### 8.3.2 查询实体

```
GET /api/graph/entities?type=string&name=string&departmentId=uuid

Response 200:
{
    "items": [
        {
            "id": "uuid",
            "name": "string",
            "type": "string",
            "properties": {},
            "departmentId": "uuid",
            "isPublic": false,
            "createdAt": "datetime"
        }
    ],
    "totalCount": 10
}
```

#### 8.3.3 图查询

```
POST /api/graph/query
Content-Type: application/json

Request:
{
    "startEntityId": "uuid",
    "endEntityId": "uuid",
    "relationType": "string",
    "maxHops": 3
}

Response 200:
{
    "paths": [
        {
            "entities": [
                { "id": "uuid", "name": "string", "type": "string" }
            ],
            "relations": [
                { "id": "uuid", "type": "string" }
            ],
            "totalHops": 1
        }
    ]
}
```

---

### 8.4 Agent API

#### 8.4.1 Agent 对话

```
POST /api/agent/chat
Content-Type: application/json

Request:
{
    "agentName": "rag-agent",
    "userInput": "string",
    "conversationId": "uuid",
    "variables": {},
    "maxTurns": 10,
    "tools": [
        {
            "name": "searchKnowledge",
            "description": "搜索知识库",
            "parametersJsonSchema": "{}"
        }
    ]
}

Response 200:
{
    "response": "string",
    "isComplete": true,
    "turns": 3,
    "toolResults": [
        {
            "toolName": "searchKnowledge",
            "parameters": "{}",
            "result": "string",
            "success": true
        }
    ]
}
```

---

## 九、配置规范

### 9.1 appsettings.json

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Warning",
            "Microsoft": "Warning"
        }
    },
    
    "QiaKon": {
        "Database": {
            "Provider": "Npgsql",
            "ConnectionString": "Host=localhost;Database=qiakon;Username=xxx;Password=xxx"
        },
        "Redis": {
            "ConnectionString": "localhost:6379",
            "InstanceName": "QiaKon"
        },
        "VectorStore": {
            "Dimension": 1536,
            "IndexType": "IvfFlat",
            "Lists": 100
        },
        "LLM": {
            "DefaultProvider": "OpenAI",
            "Providers": {
                "OpenAI": {
                    "ApiKey": "${OPENAI_API_KEY}",
                    "BaseUrl": "https://api.openai.com/v1",
                    "Model": "gpt-4o",
                    "MaxRetries": 3
                }
            }
        }
    }
}
```

---

## 十、日志规范

### 10.1 日志级别

| 级别        | 使用场景                                 |
| ----------- | ---------------------------------------- |
| **Warning** | 业务异常（权限不足、资源不存在、超时）   |
| **Error**   | 系统异常（数据库连接失败、LLM 调用失败） |
| **Info**    | 审计日志（用户登录、文档上传、权限变更） |

### 10.2 审计日志格式

```json
{
    "timestamp": "2026-04-28T10:00:00Z",
    "level": "Info",
    "category": "Audit",
    "userId": "uuid",
    "userName": "string",
    "action": "Document.Upload",
    "resourceType": "Document",
    "resourceId": "uuid",
    "result": "Success",
    "ipAddress": "string",
    "userAgent": "string"
}
```

### 10.3 应用日志格式

```json
{
    "timestamp": "2026-04-28T10:00:00Z",
    "level": "Warning",
    "category": "Retrieval",
    "message": "向量检索超时",
    "context": {
        "query": "string",
        "duration": 5000,
        "exception": "TaskCanceledException"
    }
}
```

---

## 十一、错误码规范

| 错误码      | HTTP 状态码 | 说明             |
| ----------- | ----------- | ---------------- |
| `AUTH_001`  | 401         | 未授权           |
| `AUTH_002`  | 403         | 权限不足         |
| `DOC_001`   | 404         | 文档不存在       |
| `DOC_002`   | 400         | 不支持的文档格式 |
| `DOC_003`   | 409         | 文档版本冲突     |
| `KG_001`    | 404         | 实体不存在       |
| `KG_002`    | 404         | 关系不存在       |
| `LLM_001`   | 502         | LLM 服务不可用   |
| `LLM_002`   | 408         | LLM 请求超时     |
| `VEC_001`   | 500         | 向量存储错误     |
| `CACHE_001` | 500         | 缓存服务错误     |

---

## 十二、验收检查清单

### 12.1 功能验收

- [ ] 文档上传支持 PDF/Word/Markdown/TXT
- [ ] 文档分块策略可配置（Semantic/Recursive/Fixed/MoE）
- [ ] 向量检索返回结果带权限过滤
- [ ] 增量索引支持版本管理
- [ ] 知识图谱支持三元组 CRUD
- [ ] 图查询支持多跳推理
- [ ] Agent 支持工具调用
- [ ] Agent 支持上下文裁剪
- [ ] 工作流支持 Step/Stage/Pipeline
- [ ] 缓存支持 L1/L2/L3 多级

### 12.2 非功能验收

- [ ] 向量检索 P99 < 100ms（10000 条数据）
- [ ] API 响应 P99 < 200ms
- [ ] 系统可用性 99.9%
- [ ] 应用日志仅 Warning 及以上
- [ ] 审计日志记录所有操作

### 12.3 安全验收

- [ ] 文档权限按 Department/AccessLevel 控制
- [ ] 图谱权限按 Department/AccessLevel 控制
- [ ] 检索结果自动过滤无权限资源
- [ ] 敏感操作（删除/权限变更）记录审计日志

---

*文档版本：v1.0*
*创建日期：2026-04-28*
