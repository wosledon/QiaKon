namespace QiaKon.Retrieval.VectorStore;

/// <summary>
/// 向量集合接口（对应向量数据库中的表/索引）
/// </summary>
public interface IVectorCollection : IAsyncDisposable
{
    /// <summary>
    /// 集合名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 向量维度
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// 确保集合已存在（如果不存在则创建）
    /// </summary>
    Task EnsureExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入或更新单条记录
    /// </summary>
    Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量插入或更新记录
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向量相似度搜索
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        VectorSearchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取单条记录
    /// </summary>
    Task<VectorRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 删除记录
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除记录
    /// </summary>
    Task DeleteBatchAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取集合中记录总数
    /// </summary>
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}
