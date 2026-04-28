namespace QiaKon.Graph.Engine;

/// <summary>
/// 图节点表示
/// </summary>
public sealed class GraphNode
{
    /// <summary>
    /// 节点唯一标识
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 节点标签（如：Person, Product）
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// 节点属性
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 图边表示
/// </summary>
public sealed class GraphEdge
{
    /// <summary>
    /// 边唯一标识
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 源节点 ID
    /// </summary>
    public required string SourceNodeId { get; set; }

    /// <summary>
    /// 目标节点 ID
    /// </summary>
    public required string TargetNodeId { get; set; }

    /// <summary>
    /// 边标签（如：KNOWS, OWNS, DEPENDS_ON）
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// 边属性
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 权重（用于路径查询）
    /// </summary>
    public double Weight { get; set; } = 1.0;
}
