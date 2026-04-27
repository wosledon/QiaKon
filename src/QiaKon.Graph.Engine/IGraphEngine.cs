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
    /// <param name="label">节点标签</param>
    /// <param name="properties">节点属性</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的节点</returns>
    Task<GraphNode> CreateNodeAsync(string label, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取节点
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点，不存在则返回 null</returns>
    Task<GraphNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新节点
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="properties">新属性</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的节点</returns>
    Task<GraphNode> UpdateNodeAsync(string nodeId, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除节点（同时删除所有关联的边）
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功删除</returns>
    Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标签获取节点
    /// </summary>
    /// <param name="label">节点标签</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>节点列表</returns>
    Task<IReadOnlyList<GraphNode>> GetNodesByLabelAsync(string label, int limit = 100, CancellationToken cancellationToken = default);

    // ========== 边操作 ==========

    /// <summary>
    /// 创建边
    /// </summary>
    /// <param name="sourceNodeId">源节点 ID</param>
    /// <param name="targetNodeId">目标节点 ID</param>
    /// <param name="label">边标签</param>
    /// <param name="properties">边属性</param>
    /// <param name="weight">权重（用于路径计算）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的边</returns>
    Task<GraphEdge> CreateEdgeAsync(string sourceNodeId, string targetNodeId, string label, Dictionary<string, object?>? properties = null, double weight = 1.0, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取边
    /// </summary>
    /// <param name="edgeId">边 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>边，不存在则返回 null</returns>
    Task<GraphEdge?> GetEdgeAsync(string edgeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除边
    /// </summary>
    /// <param name="edgeId">边 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功删除</returns>
    Task<bool> DeleteEdgeAsync(string edgeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取节点关联的边
    /// </summary>
    /// <param name="nodeId">节点 ID</param>
    /// <param name="direction">方向：in/out/null(双向)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>边列表</returns>
    Task<IReadOnlyList<GraphEdge>> GetEdgesByNodeAsync(string nodeId, string? direction = null, CancellationToken cancellationToken = default);

    // ========== 图遍历 ==========

    /// <summary>
    /// 广度优先遍历（BFS）
    /// </summary>
    /// <param name="startNodeId">起始节点 ID</param>
    /// <param name="edgeLabel">边标签过滤（可选）</param>
    /// <param name="maxDepth">最大深度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>遍历经过的节点列表</returns>
    Task<IReadOnlyList<GraphNode>> TraverseBfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// 深度优先遍历（DFS）
    /// </summary>
    /// <param name="startNodeId">起始节点 ID</param>
    /// <param name="edgeLabel">边标签过滤（可选）</param>
    /// <param name="maxDepth">最大深度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>遍历经过的节点列表</returns>
    Task<IReadOnlyList<GraphNode>> TraverseDfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default);

    // ========== 路径查询 ==========

    /// <summary>
    /// 最短路径查询（无权 BFS）
    /// </summary>
    /// <param name="startNodeId">起始节点 ID</param>
    /// <param name="endNodeId">目标节点 ID</param>
    /// <param name="edgeLabel">边标签过滤（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>路径节点 ID 列表，空表示无路径</returns>
    Task<IReadOnlyList<string>> ShortestPathAsync(string startNodeId, string endNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default);
}
