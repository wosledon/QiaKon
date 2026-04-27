namespace QiaKon.Retrieval.VectorStore;

/// <summary>
/// 向量存储接口
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// 获取或创建向量集合
    /// </summary>
    /// <param name="name">集合名称</param>
    /// <param name="dimensions">向量维度</param>
    Task<IVectorCollection> GetOrCreateCollectionAsync(
        string name,
        int dimensions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定集合
    /// </summary>
    /// <param name="name">集合名称</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteCollectionAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出所有集合名称
    /// </summary>
    Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default);
}
