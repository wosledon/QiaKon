namespace QiaKon.Graph.Engine;

/// <summary>
/// 图数据库引擎接口
/// </summary>
public interface IGraphEngine : IAsyncDisposable
{
    /// <summary>
    /// 初始化图引擎
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    // ========== 节点操作 ==========

    /// <summary>
    /// 创建节点
    /// </summary>
    Task<GraphNode> CreateNodeAsync(string label, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取节点
    /// </summary>
    Task<GraphNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新节点属性
    /// </summary>
    Task<GraphNode> UpdateNodeAsync(string nodeId, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除节点（同时删除所有关联的边）
    /// </summary>
    Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标签获取节点（分页）
    /// </summary>
    Task<IReadOnlyList<GraphNode>> GetNodesByLabelAsync(string label, int offset = 0, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标签统计节点数量
    /// </summary>
    Task<long> CountNodesByLabelAsync(string label, CancellationToken cancellationToken = default);

    // ========== 边操作 ==========

    /// <summary>
    /// 创建边
    /// </summary>
    Task<GraphEdge> CreateEdgeAsync(string sourceNodeId, string targetNodeId, string label, Dictionary<string, object?>? properties = null, double weight = 1.0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取边
    /// </summary>
    Task<GraphEdge?> GetEdgeAsync(string edgeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新边属性
    /// </summary>
    Task<GraphEdge> UpdateEdgeAsync(string edgeId, Dictionary<string, object?>? properties = null, double? weight = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除边
    /// </summary>
    Task<bool> DeleteEdgeAsync(string edgeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取节点关联的边（分页）
    /// </summary>
    Task<IReadOnlyList<GraphEdge>> GetEdgesByNodeAsync(string nodeId, string? direction = null, int offset = 0, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计节点关联的边数量
    /// </summary>
    Task<long> CountEdgesByNodeAsync(string nodeId, string? direction = null, CancellationToken cancellationToken = default);

    // ========== 图遍历 ==========

    /// <summary>
    /// 广度优先遍历（BFS）
    /// </summary>
    Task<IReadOnlyList<GraphNode>> TraverseBfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// 深度优先遍历（DFS）
    /// </summary>
    Task<IReadOnlyList<GraphNode>> TraverseDfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default);

    // ========== 路径查询 ==========

    /// <summary>
    /// 最短路径查询（无权 BFS）
    /// </summary>
    Task<IReadOnlyList<string>> ShortestPathAsync(string startNodeId, string endNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default);

    // ========== 批量操作 ==========

    /// <summary>
    /// 批量创建节点（事务支持）
    /// </summary>
    Task<IReadOnlyList<GraphNode>> BatchCreateNodesAsync(
        IEnumerable<(string Label, Dictionary<string, object?>? Properties)> nodes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取节点
    /// </summary>
    Task<IReadOnlyList<GraphNode>> BatchGetNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量创建边（事务支持）
    /// </summary>
    Task<IReadOnlyList<GraphEdge>> BatchCreateEdgesAsync(
        IEnumerable<(string SourceNodeId, string TargetNodeId, string Label, Dictionary<string, object?>? Properties, double Weight)> edges,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除节点
    /// </summary>
    Task<int> BatchDeleteNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除边
    /// </summary>
    Task<int> BatchDeleteEdgesAsync(IEnumerable<string> edgeIds, CancellationToken cancellationToken = default);

    // ========== 带权路径算法 ==========

    /// <summary>
    /// Dijkstra 单源最短路径（带权）
    /// </summary>
    Task<IReadOnlyList<(string NodeId, double Distance)>> DijkstraAsync(
        string startNodeId,
        string? edgeLabel = null,
        CancellationToken cancellationToken = default);

    // ========== 图统计 ==========

    /// <summary>
    /// 获取图统计信息
    /// </summary>
    Task<GraphStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    // ========== 事务支持 ==========

    /// <summary>
    /// 执行图操作事务
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="action">事务内操作，接受 IGraphEngine 实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事务结果</returns>
    Task<T> ExecuteInTransactionAsync<T>(Func<IGraphEngine, Task<T>> action, CancellationToken cancellationToken = default);
}

/// <summary>
/// 图统计信息
/// </summary>
public sealed class GraphStatistics
{
    /// <summary>
    /// 节点总数
    /// </summary>
    public long TotalNodes { get; set; }

    /// <summary>
    /// 边总数
    /// </summary>
    public long TotalEdges { get; set; }

    /// <summary>
    /// 节点标签分布
    /// </summary>
    public Dictionary<string, long> NodesByLabel { get; set; } = new();

    /// <summary>
    /// 边标签分布
    /// </summary>
    public Dictionary<string, long> EdgesByLabel { get; set; } = new();

    /// <summary>
    /// 孤立节点数量（无边关联）
    /// </summary>
    public long OrphanNodes { get; set; }

    /// <summary>
    /// 平均节点度数
    /// </summary>
    public double AverageNodeDegree { get; set; }
}
