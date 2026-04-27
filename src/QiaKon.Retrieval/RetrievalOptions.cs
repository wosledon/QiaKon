using QiaKon.Retrieval.VectorStore;

namespace QiaKon.Retrieval;

/// <summary>
/// RAG检索选项
/// </summary>
public sealed record RetrievalOptions
{
    /// <summary>
    /// 返回最相似的结果数量（默认 5）
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// 最小相似度阈值（0~1，可选）
    /// </summary>
    public float? MinSimilarity { get; init; }

    /// <summary>
    /// 是否返回完整的文档内容（而不仅是块）
    /// </summary>
    public bool IncludeDocument { get; init; } = true;

    /// <summary>
    /// 附加过滤条件（由具体实现解析）
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// 距离度量方式（默认余弦距离）
    /// </summary>
    public DistanceMetric DistanceMetric { get; init; } = DistanceMetric.CosineDistance;
}
