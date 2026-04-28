# QiaKon.Retrieval.VectorStore.Npgsql - AGENTS.md

> **模块**: PostgreSQL pgvector 向量存储  
> **职责**: PostgreSQL pgvector 扩展实现向量存储与检索  
> **依赖**: `QiaKon.Retrieval.VectorStore`, `QiaKon.Connector.Npgsql`  
> **被依赖**: `QiaKon.Retrieval`

---

## 一、模块职责

本模块使用 PostgreSQL pgvector 扩展实现向量存储，支持高效相似度搜索。

**核心职责**:
- `NpgsqlVectorStore` 实现
- `NpgsqlVectorCollection` 集合管理
- HNSW/IVFFlat 索引
- 元数据过滤查询

---

## 二、核心实现

### 2.1 表结构

```sql
CREATE TABLE IF NOT EXISTS vector_collection_{name} (
    id UUID PRIMARY KEY,
    vector VECTOR({dimension}),
    content TEXT NOT NULL,
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- HNSW 索引（推荐）
CREATE INDEX ON vector_collection_{name} 
    USING hnsw (vector vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
```

### 2.2 NpgsqlVectorStoreOptions

```csharp
public class NpgsqlVectorStoreOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Schema { get; set; } = "public";
    public int MaxPoolSize { get; set; } = 100;
    public string DefaultMetric { get; set; } = "cosine";
}
```

---

## 三、搜索实现

### 3.1 相似度查询

```sql
SELECT id, content, metadata, 
       1 - (vector <=> @query_vector) AS score
FROM vector_collection_{name}
WHERE metadata @> @filters::jsonb
ORDER BY vector <=> @query_vector
LIMIT @topK;
```

### 3.2 元数据过滤

使用 PostgreSQL JSONB 操作符：

- `@>`: 包含
- `?`: 存在键
- `->>`: 获取值

---

## 四、开发规范

### 4.1 索引选择

| 索引类型 | 适用场景            | 性能           |
| -------- | ------------------- | -------------- |
| HNSW     | 大规模数据（>10万） | 快，内存占用高 |
| IVFFlat  | 中等规模数据        | 中等           |
| 线性扫描 | 小规模数据（<1万）  | 慢，精确       |

### 4.2 批量插入

```csharp
public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken ct)
{
    using var conn = await _dataSource.OpenConnectionAsync(ct);
    using var tx = await conn.BeginTransactionAsync(ct);
    
    foreach (var batch in records.Chunk(1000))
    {
        await using var writer = await conn.BeginBinaryImportAsync(
            $"COPY vector_collection_{_name} (id, vector, content, metadata) FROM STDIN (FORMAT BINARY)", ct);
        
        foreach (var record in batch)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(record.Id, ct);
            await writer.WriteAsync(record.Vector.ToArray(), NpgsqlDbType.Real | NpgsqlDbType.Array, ct);
            await writer.WriteAsync(record.Content, ct);
            await writer.WriteAsync(record.Metadata, NpgsqlDbType.Jsonb, ct);
        }
        
        await writer.CompleteAsync(ct);
    }
    
    await tx.CommitAsync(ct);
}
```

---

## 五、测试要求

- 向量插入和查询
- 相似度搜索准确性
- 索引性能
- 并发安全

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
