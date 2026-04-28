using System.Collections.Concurrent;
using QiaKon.Graph.Engine;

namespace QiaKon.Graph.Engine.Memory;

/// <summary>
/// 内存图引擎 - 用于测试和轻量级场景
/// </summary>
public sealed class MemoryGraphEngine : IGraphEngine
{
    private readonly ConcurrentDictionary<string, GraphNode> _nodes = new();
    private readonly ConcurrentDictionary<string, GraphEdge> _edges = new();
    private readonly ConcurrentDictionary<string, List<string>> _nodeOutEdges = new();
    private readonly ConcurrentDictionary<string, List<string>> _nodeInEdges = new();
    private bool _disposed;

    private static List<string> GetOrDefault(ConcurrentDictionary<string, List<string>> dict, string key)
    {
        return dict.TryGetValue(key, out var list) ? list : new List<string>();
    }

    private static T? GetOrDefault<T>(ConcurrentDictionary<string, T> dict, string key) where T : class
    {
        return dict.TryGetValue(key, out var value) ? value : null;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    // ========== 节点操作 ==========

    public Task<GraphNode> CreateNodeAsync(string label, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
    {
        var node = new GraphNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Label = label,
            Properties = properties ?? new(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _nodes[node.Id] = node;
        _nodeOutEdges[node.Id] = new List<string>();
        _nodeInEdges[node.Id] = new List<string>();

        return Task.FromResult(node);
    }

    public Task<GraphNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

    public Task<GraphNode> UpdateNodeAsync(string nodeId, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
    {
        if (!_nodes.TryGetValue(nodeId, out var existing))
            throw new KeyNotFoundException($"Node with id '{nodeId}' not found");

        existing.Properties = properties ?? new();
        existing.UpdatedAt = DateTime.UtcNow;

        return Task.FromResult(existing);
    }

    public Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_nodes.TryRemove(nodeId, out _))
            return Task.FromResult(false);

        // 删除所有关联的边
        if (_nodeOutEdges.TryRemove(nodeId, out var outEdges))
        {
            foreach (var edgeId in outEdges)
            {
                _edges.TryRemove(edgeId, out _);
            }
        }

        if (_nodeInEdges.TryRemove(nodeId, out var inEdges))
        {
            foreach (var edgeId in inEdges)
            {
                _edges.TryRemove(edgeId, out _);
            }
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<GraphNode>> GetNodesByLabelAsync(string label, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var nodes = _nodes.Values
            .Where(n => n.Label == label)
            .OrderBy(n => n.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<GraphNode>>(nodes);
    }

    public Task<long> CountNodesByLabelAsync(string label, CancellationToken cancellationToken = default)
    {
        var count = _nodes.Values.Count(n => n.Label == label);
        return Task.FromResult((long)count);
    }

    // ========== 边操作 ==========

    public Task<GraphEdge> CreateEdgeAsync(string sourceNodeId, string targetNodeId, string label, Dictionary<string, object?>? properties = null, double weight = 1.0, CancellationToken cancellationToken = default)
    {
        if (!_nodes.ContainsKey(sourceNodeId))
            throw new KeyNotFoundException($"Source node '{sourceNodeId}' not found");
        if (!_nodes.ContainsKey(targetNodeId))
            throw new KeyNotFoundException($"Target node '{targetNodeId}' not found");

        var edge = new GraphEdge
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            Label = label,
            Properties = properties ?? new(),
            Weight = weight,
            CreatedAt = DateTime.UtcNow
        };

        _edges[edge.Id] = edge;

        _nodeOutEdges.GetOrAdd(sourceNodeId, _ => new List<string>()).Add(edge.Id);
        _nodeInEdges.GetOrAdd(targetNodeId, _ => new List<string>()).Add(edge.Id);

        return Task.FromResult(edge);
    }

    public Task<GraphEdge?> GetEdgeAsync(string edgeId, CancellationToken cancellationToken = default)
    {
        _edges.TryGetValue(edgeId, out var edge);
        return Task.FromResult(edge);
    }

    public Task<GraphEdge> UpdateEdgeAsync(string edgeId, Dictionary<string, object?>? properties = null, double? weight = null, CancellationToken cancellationToken = default)
    {
        if (!_edges.TryGetValue(edgeId, out var existing))
            throw new KeyNotFoundException($"Edge with id '{edgeId}' not found");

        if (properties != null)
            existing.Properties = properties;
        if (weight.HasValue)
            existing.Weight = weight.Value;

        return Task.FromResult(existing);
    }

    public Task<bool> DeleteEdgeAsync(string edgeId, CancellationToken cancellationToken = default)
    {
        if (!_edges.TryRemove(edgeId, out var edge))
            return Task.FromResult(false);

        // 从节点的边列表中移除
        if (_nodeOutEdges.TryGetValue(edge.SourceNodeId, out var outList))
            outList.Remove(edgeId);
        if (_nodeInEdges.TryGetValue(edge.TargetNodeId, out var inList))
            inList.Remove(edgeId);

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<GraphEdge>> GetEdgesByNodeAsync(string nodeId, string? direction = null, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var edgeIds = direction?.ToLowerInvariant() switch
        {
            "out" => GetOrDefault(_nodeOutEdges, nodeId),
            "in" => GetOrDefault(_nodeInEdges, nodeId),
            _ => GetOrDefault(_nodeOutEdges, nodeId)
                     .Concat(GetOrDefault(_nodeInEdges, nodeId))
                     .Distinct()
                     .ToList()
        };

        var edges = edgeIds
            .Skip(offset)
            .Take(limit)
            .Select(id => GetOrDefault(_edges, id))
            .Where(e => e != null)
            .Cast<GraphEdge>()
            .ToList();

        return Task.FromResult<IReadOnlyList<GraphEdge>>(edges);
    }

    public Task<long> CountEdgesByNodeAsync(string nodeId, string? direction = null, CancellationToken cancellationToken = default)
    {
        var count = direction?.ToLowerInvariant() switch
        {
            "out" => GetOrDefault(_nodeOutEdges, nodeId).Count,
            "in" => GetOrDefault(_nodeInEdges, nodeId).Count,
            _ => GetOrDefault(_nodeOutEdges, nodeId).Count +
                 GetOrDefault(_nodeInEdges, nodeId).Count
        };

        return Task.FromResult((long)count);
    }

    // ========== 图遍历 ==========

    public Task<IReadOnlyList<GraphNode>> TraverseBfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
    {
        var result = new List<GraphNode>();
        var visited = new HashSet<string> { startNodeId };
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;

            if (_nodes.TryGetValue(currentId, out var node))
                result.Add(node);

            var neighborIds = GetNeighborIds(currentId, edgeLabel);
            foreach (var neighborId in neighborIds)
            {
                if (visited.Add(neighborId))
                    queue.Enqueue((neighborId, depth + 1));
            }
        }

        return Task.FromResult<IReadOnlyList<GraphNode>>(result);
    }

    public Task<IReadOnlyList<GraphNode>> TraverseDfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
    {
        var result = new List<GraphNode>();
        var visited = new HashSet<string>();
        TraverseDfsRecursive(startNodeId, edgeLabel, maxDepth, 0, visited, result);
        return Task.FromResult<IReadOnlyList<GraphNode>>(result);
    }

    private void TraverseDfsRecursive(string nodeId, string? edgeLabel, int maxDepth, int currentDepth, HashSet<string> visited, List<GraphNode> result)
    {
        if (currentDepth >= maxDepth || visited.Contains(nodeId)) return;
        visited.Add(nodeId);

        if (_nodes.TryGetValue(nodeId, out var node))
            result.Add(node);

        var neighborIds = GetNeighborIds(nodeId, edgeLabel);
        foreach (var neighborId in neighborIds)
        {
            TraverseDfsRecursive(neighborId, edgeLabel, maxDepth, currentDepth + 1, visited, result);
        }
    }

    private List<string> GetNeighborIds(string nodeId, string? edgeLabel)
    {
        var result = new List<string>();
        var outEdges = GetOrDefault(_nodeOutEdges, nodeId);

        foreach (var edgeId in outEdges)
        {
            if (_edges.TryGetValue(edgeId, out var edge))
            {
                if (edgeLabel == null || edge.Label == edgeLabel)
                    result.Add(edge.TargetNodeId);
            }
        }

        return result;
    }

    // ========== 路径查询 ==========

    public Task<IReadOnlyList<string>> ShortestPathAsync(string startNodeId, string endNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<string> { startNodeId };
        var queue = new Queue<List<string>>();
        queue.Enqueue(new List<string> { startNodeId });

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var currentId = path[^1];

            if (currentId == endNodeId)
                return Task.FromResult<IReadOnlyList<string>>(path);

            var neighborIds = GetNeighborIds(currentId, edgeLabel);
            foreach (var neighborId in neighborIds)
            {
                if (visited.Add(neighborId))
                    queue.Enqueue(new List<string>(path) { neighborId });
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    // ========== 批量操作 ==========

    public Task<IReadOnlyList<GraphNode>> BatchCreateNodesAsync(IEnumerable<(string Label, Dictionary<string, object?>? Properties)> nodes, CancellationToken cancellationToken = default)
    {
        var created = new List<GraphNode>();
        foreach (var (label, properties) in nodes)
        {
            var node = new GraphNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Label = label,
                Properties = properties ?? new(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _nodes[node.Id] = node;
            _nodeOutEdges[node.Id] = new List<string>();
            _nodeInEdges[node.Id] = new List<string>();
            created.Add(node);
        }
        return Task.FromResult<IReadOnlyList<GraphNode>>(created);
    }

    public Task<IReadOnlyList<GraphNode>> BatchGetNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var nodes = nodeIds
            .Select(id => GetOrDefault(_nodes, id))
            .Where(n => n != null)
            .Cast<GraphNode>()
            .ToList();
        return Task.FromResult<IReadOnlyList<GraphNode>>(nodes);
    }

    public Task<IReadOnlyList<GraphEdge>> BatchCreateEdgesAsync(IEnumerable<(string SourceNodeId, string TargetNodeId, string Label, Dictionary<string, object?>? Properties, double Weight)> edges, CancellationToken cancellationToken = default)
    {
        var created = new List<GraphEdge>();
        foreach (var (src, tgt, label, properties, weight) in edges)
        {
            if (!_nodes.ContainsKey(src))
                throw new KeyNotFoundException($"Source node '{src}' not found");
            if (!_nodes.ContainsKey(tgt))
                throw new KeyNotFoundException($"Target node '{tgt}' not found");

            var edge = new GraphEdge
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceNodeId = src,
                TargetNodeId = tgt,
                Label = label,
                Properties = properties ?? new(),
                Weight = weight,
                CreatedAt = DateTime.UtcNow
            };
            _edges[edge.Id] = edge;
            _nodeOutEdges.GetOrAdd(src, _ => new List<string>()).Add(edge.Id);
            _nodeInEdges.GetOrAdd(tgt, _ => new List<string>()).Add(edge.Id);
            created.Add(edge);
        }
        return Task.FromResult<IReadOnlyList<GraphEdge>>(created);
    }

    public Task<int> BatchDeleteNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var id in nodeIds)
        {
            if (DeleteNodeAsync(id, CancellationToken.None).Result)
                count++;
        }
        return Task.FromResult(count);
    }

    public Task<int> BatchDeleteEdgesAsync(IEnumerable<string> edgeIds, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var id in edgeIds)
        {
            if (DeleteEdgeAsync(id, CancellationToken.None).Result)
                count++;
        }
        return Task.FromResult(count);
    }

    // ========== 带权路径算法 ==========

    public Task<IReadOnlyList<(string NodeId, double Distance)>> DijkstraAsync(string startNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default)
    {
        var distances = new Dictionary<string, double> { [startNodeId] = 0 };
        var visited = new HashSet<string>();
        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(startNodeId, 0);

        while (queue.Count > 0)
        {
            if (!queue.TryDequeue(out var currentId, out var currentDist))
                continue;
            if (visited.Contains(currentId)) continue;
            visited.Add(currentId);

            foreach (var edgeId in GetOrDefault(_nodeOutEdges, currentId))
            {
                if (!_edges.TryGetValue(edgeId, out var edge))
                    continue;
                if (edgeLabel != null && edge.Label != edgeLabel)
                    continue;

                var newDist = currentDist + edge.Weight;
                if (!distances.TryGetValue(edge.TargetNodeId, out var existingDist) || newDist < existingDist)
                {
                    distances[edge.TargetNodeId] = newDist;
                    queue.Enqueue(edge.TargetNodeId, newDist);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<(string, double)>>(
            distances.Select(kv => (kv.Key, kv.Value)).ToList());
    }

    // ========== 图统计 ==========

    public Task<GraphStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new GraphStatistics
        {
            TotalNodes = _nodes.Count,
            TotalEdges = _edges.Count,
            NodesByLabel = _nodes.Values.GroupBy(n => n.Label).ToDictionary(g => g.Key, g => (long)g.Count()),
            EdgesByLabel = _edges.Values.GroupBy(e => e.Label).ToDictionary(g => g.Key, g => (long)g.Count()),
            OrphanNodes = _nodes.Values.LongCount(n =>
                !(GetOrDefault(_nodeOutEdges, n.Id).Count > 0 ||
                  GetOrDefault(_nodeInEdges, n.Id).Count > 0)),
            AverageNodeDegree = _nodes.Count > 0 ? (double)_edges.Count * 2 / _nodes.Count : 0
        };

        return Task.FromResult(stats);
    }

    // ========== 事务支持 ==========

    public Task<T> ExecuteInTransactionAsync<T>(Func<IGraphEngine, Task<T>> action, CancellationToken cancellationToken = default)
        => action(this);

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _nodes.Clear();
        _edges.Clear();
        _nodeOutEdges.Clear();
        _nodeInEdges.Clear();
        return ValueTask.CompletedTask;
    }
}
