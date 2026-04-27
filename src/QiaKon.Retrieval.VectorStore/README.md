# QiaKon.Retrieval.VectorStore

向量存储抽象层，定义向量数据库的通用接口。

## 定位

本模块是 RAG 系统的**向量存储契约层**，不绑定任何具体的向量数据库实现（如 pgvector、Milvus、Qdrant 等）。

## 核心概念

| 概念 | 接口 | 说明 |
|------|------|------|
| 向量存储 | `IVectorStore` | 管理多个向量集合的入口 |
| 向量集合 | `IVectorCollection` | 对应数据库中的表/索引，存储同维度向量 |
| 向量记录 | `VectorRecord` | 单条数据：Id + Embedding + Text + Metadata |
| 搜索结果 | `VectorSearchResult` | 相似度搜索返回的结果 |

## 核心接口

```csharp
// 向量存储
public interface IVectorStore
{
    Task<IVectorCollection> GetOrCreateCollectionAsync(string name, int dimensions);
    Task<bool> DeleteCollectionAsync(string name);
    Task<IReadOnlyList<string>> ListCollectionsAsync();
}

// 向量集合
public interface IVectorCollection : IAsyncDisposable
{
    string Name { get; }
    int Dimensions { get; }

    Task UpsertAsync(VectorRecord record);
    Task UpsertBatchAsync(IEnumerable<VectorRecord> records);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(ReadOnlyMemory<float> embedding, VectorSearchOptions? options);
}
```

## 距离度量

```csharp
public enum DistanceMetric
{
    CosineDistance,   // 余弦距离（默认，推荐）
    Euclidean,        // 欧氏距离
    InnerProduct      // 内积
}
```

## 使用方式

由具体实现模块注册（如 `QiaKon.Retrieval.VectorStore.Npgsql`）：

```csharp
services.AddNpgsqlVectorStore(options =>
{
    options.ConnectionString = "Host=...;Database=...";
});
```
