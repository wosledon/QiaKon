namespace QiaKon.Retrieval.VectorStore;

/// <summary>
/// 向量搜索结果
/// </summary>
public sealed record VectorSearchResult
{
    /// <summary>
    /// 匹配的记录
    /// </summary>
    public required VectorRecord Record { get; init; }

    /// <summary>
    /// 相似度得分（0~1，越高越相似）
    /// </summary>
    public float Score { get; init; }

    /// <summary>
    /// 原始距离值（由具体实现决定度量方式）
    /// </summary>
    public float Distance { get; init; }
}
