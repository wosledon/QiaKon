# QiaKon.Retrieval.VectorStore.Npgsql

PostgreSQL + pgvector 向量存储实现。

## 定位

基于 PostgreSQL 的 `pgvector` 扩展实现 `IVectorStore` 接口，提供生产级的向量存储与相似度搜索能力。

## 前置要求

1. PostgreSQL 12+ 并已安装 [pgvector](https://github.com/pgvector/pgvector) 扩展
2. 数据库用户具有创建扩展和表的权限

```sql
-- 在目标数据库中启用 pgvector
CREATE EXTENSION IF NOT EXISTS vector;
```

## 快速开始

```csharp
services.AddNpgsqlVectorStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=qiakon;Username=postgres;Password=xxx";
    options.AutoCreateExtension = true;  // 自动创建 pgvector 扩展
    options.AutoCreateTables = true;     // 自动创建集合表
    options.MaxPoolSize = 20;
});
```

## 配置选项

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `ConnectionString` | — | PostgreSQL 连接字符串（必填） |
| `AutoCreateExtension` | `true` | 自动创建 pgvector 扩展 |
| `AutoCreateTables` | `true` | 自动创建集合表 |
| `MaxPoolSize` | `20` | 连接池最大大小 |
| `CommandTimeoutSeconds` | `30` | SQL 命令超时时间 |

## 存储结构

每个向量集合对应一张 PostgreSQL 表：

```sql
CREATE TABLE IF NOT EXISTS vector_collection_{name} (
    id UUID PRIMARY KEY,
    embedding VECTOR({dimensions}),
    text TEXT,
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 自动创建 IVF 或 HNSW 索引（取决于配置）
CREATE INDEX ON vector_collection_{name} 
USING ivfflat (embedding vector_cosine_ops);
```

## 注意事项

- `Dimensions` 必须与 `IEmbeddingService.Dimensions` 一致
- 大规模数据建议使用 HNSW 索引替代 IVF，查询速度更快
- `Metadata` 使用 JSONB 存储，支持灵活的过滤查询
