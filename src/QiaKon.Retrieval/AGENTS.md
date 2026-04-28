# QiaKon.Retrieval - AGENTS.md

> **模块**: 检索管道  
> **职责**: 文档处理、分块、嵌入生成、向量检索  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Llm.*`, `QiaKon.Workflow`, `QiaKon.Connector.*`  
> **被依赖**: `QiaKon.Api`

---

## 一、模块职责

本模块实现 RAG（检索增强生成）的检索管道，支持文档处理、智能分块、向量嵌入和混合检索。

**核心职责**:
- 文档解析与处理（PDF/Word/Markdown/TXT）
- 智能分块（MoE 策略路由）
- 向量嵌入生成
- 向量存储与检索（PostgreSQL pgvector）
- 混合检索（向量+关键词+图谱）

---

## 二、子模块架构

### 2.1 子模块总览

| 子模块                                | 职责                     | 文档                                                          |
| ------------------------------------- | ------------------------ | ------------------------------------------------------------- |
| `QiaKon.Retrieval.DocumentProcessor`  | 文档解析与处理           | [AGENTS.md](../QiaKon.Retrieval.DocumentProcessor/AGENTS.md)  |
| `QiaKon.Retrieval.Chunking`           | 分块策略抽象             | [AGENTS.md](../QiaKon.Retrieval.Chunking/AGENTS.md)           |
| `QiaKon.Retrieval.Chunking.MoE`       | MoE 分块路由             | [AGENTS.md](../QiaKon.Retrieval.Chunking.MoE/AGENTS.md)       |
| `QiaKon.Retrieval.Embedding`          | 向量嵌入生成             | [AGENTS.md](../QiaKon.Retrieval.Embedding/AGENTS.md)          |
| `QiaKon.Retrieval.VectorStore`        | 向量存储抽象             | [AGENTS.md](../QiaKon.Retrieval.VectorStore/AGENTS.md)        |
| `QiaKon.Retrieval.VectorStore.Npgsql` | PostgreSQL pgvector 实现 | [AGENTS.md](../QiaKon.Retrieval.VectorStore.Npgsql/AGENTS.md) |

### 2.2 依赖关系

```
QiaKon.Retrieval
├── DocumentProcessor (文档解析)
├── Chunking (分块抽象)
│   └── MoE (专家路由)
├── Embedding (嵌入生成)
└── VectorStore (向量存储抽象)
    └── Npgsql (pgvector 实现)
```

---

## 三、核心接口

### 3.1 文档接口

```csharp
public interface IDocument
{
    Guid Id { get; }
    string Title { get; }
    string Content { get; }
    IReadOnlyDictionary<string, object>? Metadata { get; }
}
```

### 3.2 文档块接口

```csharp
public interface IChunk
{
    Guid Id { get; }
    Guid DocumentId { get; }
    string Content { get; }
    int Order { get; }
    ReadOnlyMemory<float> Embedding { get; }
}
```

### 3.3 分块策略接口

```csharp
public interface IChunkingStrategy
{
    string Name { get; }
    Task<IReadOnlyList<IChunk>> ChunkAsync(IDocument document, ChunkingOptions options, CancellationToken ct = default);
}
```

### 3.4 向量存储接口

```csharp
public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<IChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<IChunk>> SearchAsync(
        ReadOnlyMemory<float> queryVector, 
        SearchOptions options, 
        CancellationToken ct = default);
    Task DeleteAsync(Guid documentId, CancellationToken ct = default);
}
```

---

## 四、分块策略

### 4.1 内置策略

| 策略                | 适用场景     | 分块方式           |
| ------------------- | ------------ | ------------------ |
| `SemanticChunking`  | 语义连贯文章 | 按语义边界切分     |
| `RecursiveChunking` | 层次结构文档 | 按标题层级递归切分 |
| `FixedSizeChunking` | 简单文本     | 按固定长度切分     |
| `TableChunking`     | 表格密集文档 | 表格独立分块       |

### 4.2 MoE 分块路由

```csharp
public class MoEChunkingStrategy : IChunkingStrategy
{
    private readonly IDictionary<DocumentType, IChunkingStrategy> _experts;
    private readonly IChunkingStrategy _defaultExpert;
    
    public async Task<IReadOnlyList<IChunk>> ChunkAsync(IDocument document, ...)
    {
        var expert = _experts.GetValueOrDefault(document.Type, _defaultExpert);
        return await expert.ChunkAsync(document, ...);
    }
}
```

### 4.3 分块配置

```json
{
  "Chunking": {
    "DefaultStrategy": "MoE",
    "Strategies": {
      "FixedSize": {
        "ChunkSize": 512,
        "Overlap": 50
      },
      "Recursive": {
        "Separators": ["\n## ", "\n### ", "\n", " "],
        "ChunkSize": 1000,
        "Overlap": 100
      }
    }
  }
}
```

---

## 五、向量存储

### 5.1 PostgreSQL pgvector

**表结构**:
```sql
CREATE TABLE chunks (
    id UUID PRIMARY KEY,
    document_id UUID NOT NULL,
    content TEXT NOT NULL,
    "order" INT NOT NULL,
    embedding VECTOR(1536),
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_chunks_embedding ON chunks 
    USING hnsw (embedding vector_cosine_ops);
```

### 5.2 检索选项

```csharp
public class SearchOptions
{
    public int TopK { get; init; } = 10;
    public float ScoreThreshold { get; init; } = 0.7f;
    public IDictionary<string, object>? Filters { get; init; }
    public bool EnableHybridSearch { get; init; }
    public bool EnableRerank { get; init; }
}
```

---

## 六、混合检索

### 6.1 检索流程

```
用户 Query
    ↓
Query 理解
    ↓
┌─────────────┬─────────────┬─────────────┐
│  向量检索   │  关键词检索  │  图谱检索   │
│ (Embedding) │   (BM25)    │  (Graph)    │
└─────────────┴─────────────┴─────────────┘
    ↓           ↓           ↓
    └───────────┴───────────┘
                ↓
           结果融合
                ↓
          重排序 (Rerank)
                ↓
         生成增强上下文
```

### 6.2 结果融合算法

```csharp
public class ResultFusion
{
    public IReadOnlyList<SearchResult> Fuse(
        IReadOnlyList<SearchResult> vectorResults,
        IReadOnlyList<SearchResult> keywordResults,
        IReadOnlyList<SearchResult> graphResults)
    {
        // 使用 RRF (Reciprocal Rank Fusion) 算法
        var fused = new Dictionary<Guid, double>();
        
        foreach (var results in new[] { vectorResults, keywordResults, graphResults })
        {
            for (int i = 0; i < results.Count; i++)
            {
                var chunkId = results[i].ChunkId;
                fused[chunkId] = fused.GetValueOrDefault(chunkId, 0) + 1.0 / (60 + i);
            }
        }
        
        return fused.OrderByDescending(x => x.Value)
            .Select(x => new SearchResult { ChunkId = x.Key, Score = x.Value })
            .ToList();
    }
}
```

---

## 七、权限过滤

检索时必须注入用户权限上下文：

```csharp
public class PermissionFilter
{
    public SearchOptions ApplyPermissions(SearchOptions options, UserContext user)
    {
        options.Filters ??= new Dictionary<string, object>();
        
        if (!user.IsAdmin)
        {
            options.Filters["department_id"] = user.DepartmentIds;
            options.Filters["is_public"] = true;
        }
        
        return options;
    }
}
```

---

## 八、开发规范

### 8.1 添加新分块策略

1. 实现 `IChunkingStrategy` 接口
2. 在 MoE 路由中注册新策略
3. 编写单元测试验证分块效果
4. 更新配置示例

### 8.2 添加新向量存储

1. 实现 `IVectorStore` 接口
2. 编写健康检查逻辑
3. 实现连接池管理
4. 编写集成测试

---

## 九、测试要求

### 9.1 单元测试

- 分块策略正确性
- 结果融合算法
- 权限过滤逻辑

### 9.2 集成测试

- 向量存储读写
- 混合检索准确性
- 增量索引逻辑

---

## 十、注意事项

1. **向量维度**: 确保嵌入维度与 pgvector 表结构一致
2. **索引优化**: 大规模数据时使用 HNSW 索引
3. **增量更新**: 文档更新时仅重新索引变更块
4. **批量操作**: 使用批量插入提升性能

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
