# QiaKon.Retrieval.VectorStore - AGENTS.md

> **模块**: 向量存储抽象  
> **职责**: 向量存储接口定义、搜索选项、结果封装  
> **依赖**: `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Retrieval.VectorStore.Npgsql`, `QiaKon.Retrieval`

---

## 一、模块职责

本模块定义向量存储的抽象接口，支持多种向量存储后端。

**核心职责**:
- `IVectorStore` 接口定义
- `IVectorCollection` 集合管理
- 搜索选项与结果封装
- 向量操作抽象

---

## 二、核心接口

### 2.1 IVectorStore

```csharp
public interface IVectorStore
{
    Task CreateCollectionAsync(string name, int dimension, CancellationToken ct = default);
    Task DeleteCollectionAsync(string name, CancellationToken ct = default);
    IVectorCollection GetCollection(string name);
}
```

### 2.2 IVectorCollection

```csharp
public interface IVectorCollection
{
    string Name { get; }
    int Dimension { get; }
    
    Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken ct = default);
    Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        VectorSearchOptions? options = null,
        CancellationToken ct = default);
}
```

### 2.3 VectorRecord

```csharp
public sealed record VectorRecord
{
    public required Guid Id { get; init; }
    public required ReadOnlyMemory<float> Vector { get; init; }
    public required string Content { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
```

### 2.4 VectorSearchOptions

```csharp
public class VectorSearchOptions
{
    public int TopK { get; init; } = 10;
    public float ScoreThreshold { get; init; } = 0.7f;
    public IDictionary<string, object>? Filters { get; init; }
    public string? Metric { get; init; } = "cosine";
}
```

### 2.5 VectorSearchResult

```csharp
public sealed record VectorSearchResult
{
    public required Guid Id { get; init; }
    public required float Score { get; init; }
    public required string Content { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
```

---

## 三、相似度度量

### 3.1 支持的度量

| 度量        | 说明       | 适用场景         |
| ----------- | ---------- | ---------------- |
| Cosine      | 余弦相似度 | 文本向量（默认） |
| Euclidean   | 欧氏距离   | 图像向量         |
| Dot Product | 点积       | 归一化向量       |

---

## 四、开发规范

### 4.1 实现新后端

1. 实现 `IVectorStore` 和 `IVectorCollection`
2. 实现相似度搜索算法
3. 实现元数据过滤
4. 编写性能测试

### 4.2 索引优化

- 大规模数据时使用近似最近邻（ANN）索引
- PostgreSQL 使用 HNSW 或 IVFFlat 索引

---

## 五、测试要求

- 向量插入和删除
- 搜索准确性
- 元数据过滤
- 性能基准测试

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
