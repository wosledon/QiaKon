namespace QiaKon.Retrieval.VectorStore;

/// <summary>
/// 向量搜索选项
/// </summary>
public sealed record VectorSearchOptions
{
    /// <summary>
    /// 返回最相似的结果数量（默认 10）
    /// </summary>
    public int TopK { get; init; } = 10;

    /// <summary>
    /// 最小相似度阈值（0~1，可选）
    /// </summary>
    public float? MinSimilarity { get; init; }

    /// <summary>
    /// 距离度量方式（默认余弦距离）
    /// </summary>
    public DistanceMetric DistanceMetric { get; init; } = DistanceMetric.CosineDistance;

    /// <summary>
    /// 附加过滤条件 SQL 片段（可选，由具体实现解析）
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// 过滤条件参数（可选）
    /// </summary>
    public Dictionary<string, object?>? FilterParameters { get; init; }
}

/// <summary>
/// 距离度量方式
/// </summary>
public enum DistanceMetric
{
    /// <summary>
    /// 欧几里得距离（L2）
    /// </summary>
    Euclidean,

    /// <summary>
    /// 余弦距离
    /// </summary>
    CosineDistance,

    /// <summary>
    /// 内积（负内积作为距离）
    /// </summary>
    InnerProduct
}
