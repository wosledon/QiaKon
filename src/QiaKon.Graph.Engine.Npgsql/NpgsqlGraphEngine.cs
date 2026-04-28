using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using QiaKon.Graph.Engine;

namespace QiaKon.Graph.Engine.Npgsql;

/// <summary>
/// PostgreSQL 图数据库引擎实现
/// </summary>
public sealed class NpgsqlGraphEngine : IGraphEngine
{
    private readonly GraphEngineOptions _options;
    private readonly ILogger<NpgsqlGraphEngine> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private NpgsqlDataSource? _dataSource;
    private bool _disposed;

    private string NodeTable => $"\"{_options.GraphName}\".\"{_options.NodeTableName}\"";
    private string EdgeTable => $"\"{_options.GraphName}\".\"{_options.EdgeTableName}\"";

    public NpgsqlGraphEngine(GraphEngineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = (options.LoggerFactory?.CreateLogger<NpgsqlGraphEngine>())
            ?? NullLogger<NpgsqlGraphEngine>.Instance;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_options.ConnectionString)
        {
            MinPoolSize = _options.MinPoolSize,
            MaxPoolSize = _options.MaxPoolSize,
            CommandTimeout = _options.CommandTimeoutSeconds,
            Pooling = true
        };

        _dataSource = NpgsqlDataSource.Create(connectionStringBuilder.ConnectionString);

        if (_options.AutoCreateSchema)
        {
            await CreateSchemaAsync(cancellationToken);
        }

        if (_options.AutoCreateIndex)
        {
            await CreateIndexesAsync(cancellationToken);
        }

        _logger.LogInformation("NpgsqlGraphEngine initialized for graph '{GraphName}'", _options.GraphName);
    }

    /// <inheritdoc />
    public async Task<GraphNode> CreateNodeAsync(string label, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var node = new GraphNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Label = label,
            Properties = properties ?? new Dictionary<string, object?>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        const string sql = @"
            INSERT INTO {{NODE_TABLE}} (id, label, properties, created_at, updated_at)
            VALUES (@id, @label, @properties::jsonb, @created_at, @updated_at)
            RETURNING id, label, properties, created_at, updated_at";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql.Replace("{{NODE_TABLE}}", NodeTable), connection);
        command.Parameters.AddWithValue("id", node.Id);
        command.Parameters.AddWithValue("label", node.Label);
        command.Parameters.AddWithValue("properties", JsonSerializer.Serialize(node.Properties));
        command.Parameters.AddWithValue("created_at", node.CreatedAt);
        command.Parameters.AddWithValue("updated_at", node.UpdatedAt);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            node = ReadNode(reader);
        }

        _logger.LogDebug("Created node {NodeId} with label {Label}", node.Id, node.Label);
        return node;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> BatchCreateNodesAsync(
        IEnumerable<(string Label, Dictionary<string, object?>? Properties)> nodes,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var nodeList = nodes.ToList();
        if (nodeList.Count == 0)
            return [];

        var createdNodes = new List<GraphNode>(nodeList.Count);

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string sql = @"
                INSERT INTO {{NODE_TABLE}} (id, label, properties, created_at, updated_at)
                VALUES (@id, @label, @properties::jsonb, @created_at, @updated_at)
                RETURNING id, label, properties, created_at, updated_at";

            var now = DateTime.UtcNow;

            foreach (var (label, properties) in nodeList)
            {
                var node = new GraphNode
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Label = label,
                    Properties = properties ?? new(),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await using var command = new NpgsqlCommand(sql.Replace("{{NODE_TABLE}}", NodeTable), connection, transaction);
                command.Parameters.AddWithValue("id", node.Id);
                command.Parameters.AddWithValue("label", node.Label);
                command.Parameters.AddWithValue("properties", JsonSerializer.Serialize(node.Properties));
                command.Parameters.AddWithValue("created_at", node.CreatedAt);
                command.Parameters.AddWithValue("updated_at", node.UpdatedAt);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    createdNodes.Add(ReadNode(reader));
                }
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Batch created {Count} nodes", createdNodes.Count);
            return createdNodes;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<GraphNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        const string sql = @"
            SELECT id, label, properties, created_at, updated_at
            FROM {{NODE_TABLE}}
            WHERE id = @id";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql.Replace("{{NODE_TABLE}}", NodeTable), connection);
        command.Parameters.AddWithValue("id", nodeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadNode(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> BatchGetNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var idList = nodeIds.ToList();
        if (idList.Count == 0)
            return [];

        const string sql = @"
            SELECT id, label, properties, created_at, updated_at
            FROM {{NODE_TABLE}}
            WHERE id = ANY(@ids)";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql.Replace("{{NODE_TABLE}}", NodeTable), connection);
        command.Parameters.AddWithValue("ids", idList.ToArray());

        var nodes = new List<GraphNode>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nodes.Add(ReadNode(reader));
        }

        return nodes;
    }

    /// <inheritdoc />
    public async Task<GraphNode> UpdateNodeAsync(string nodeId, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var updatedAt = DateTime.UtcNow;
        const string sql = @"
            UPDATE {{NODE_TABLE}}
            SET properties = @properties::jsonb, updated_at = @updated_at
            WHERE id = @id
            RETURNING id, label, properties, created_at, updated_at";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql.Replace("{{NODE_TABLE}}", NodeTable), connection);
        command.Parameters.AddWithValue("id", nodeId);
        command.Parameters.AddWithValue("properties", JsonSerializer.Serialize(properties ?? new Dictionary<string, object?>()));
        command.Parameters.AddWithValue("updated_at", updatedAt);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            _logger.LogDebug("Updated node {NodeId}", nodeId);
            return ReadNode(reader);
        }

        throw new KeyNotFoundException($"Node with id '{nodeId}' not found");
    }

    /// <inheritdoc />
    public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            // 先删除所有关联的边 (ON DELETE CASCADE 已经自动处理，但显式删除更安全)
            var deleteEdgesSql = $"DELETE FROM {EdgeTable} WHERE source_node_id = @id OR target_node_id = @id";

            await using (var command = new NpgsqlCommand(deleteEdgesSql, connection, transaction))
            {
                command.Parameters.AddWithValue("id", nodeId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // 删除节点
            var deleteNodeSql = $"DELETE FROM {NodeTable} WHERE id = @id";
            await using (var command = new NpgsqlCommand(deleteNodeSql, connection, transaction))
            {
                command.Parameters.AddWithValue("id", nodeId);
                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogDebug("Deleted node {NodeId}", nodeId);
                return rowsAffected > 0;
            }
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> GetNodesByLabelAsync(string label, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        const string sql = @"
            SELECT id, label, properties, created_at, updated_at
            FROM {{NODE_TABLE}}
            WHERE label = @label
            ORDER BY created_at
            OFFSET @offset LIMIT @limit";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql.Replace("{{NODE_TABLE}}", NodeTable), connection);
        command.Parameters.AddWithValue("label", label);
        command.Parameters.AddWithValue("offset", offset);
        command.Parameters.AddWithValue("limit", limit);

        var nodes = new List<GraphNode>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nodes.Add(ReadNode(reader));
        }

        return nodes;
    }

    /// <inheritdoc />
    public async Task<long> CountNodesByLabelAsync(string label, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        const string sql = @"SELECT COUNT(*) FROM {{NODE_TABLE}} WHERE label = @label";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql.Replace("{{NODE_TABLE}}", NodeTable), connection);
        command.Parameters.AddWithValue("label", label);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    /// <inheritdoc />
    public async Task<GraphEdge> CreateEdgeAsync(string sourceNodeId, string targetNodeId, string label, Dictionary<string, object?>? properties = null, double weight = 1.0, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var edge = new GraphEdge
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            Label = label,
            Properties = properties ?? new Dictionary<string, object?>(),
            Weight = weight,
            CreatedAt = DateTime.UtcNow
        };

        const string sql = @"
            INSERT INTO {{EDGE_TABLE}} (id, source_node_id, target_node_id, label, properties, weight, created_at)
            VALUES (@id, @source_node_id, @target_node_id, @label, @properties::jsonb, @weight, @created_at)
            RETURNING id, source_node_id, target_node_id, label, properties, weight, created_at";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = new NpgsqlCommand(sql.Replace("{{EDGE_TABLE}}", EdgeTable), connection);
            command.Parameters.AddWithValue("id", edge.Id);
            command.Parameters.AddWithValue("source_node_id", edge.SourceNodeId);
            command.Parameters.AddWithValue("target_node_id", edge.TargetNodeId);
            command.Parameters.AddWithValue("label", edge.Label);
            command.Parameters.AddWithValue("properties", JsonSerializer.Serialize(edge.Properties));
            command.Parameters.AddWithValue("weight", edge.Weight);
            command.Parameters.AddWithValue("created_at", edge.CreatedAt);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                edge = ReadEdge(reader);
            }

            _logger.LogDebug("Created edge {EdgeId} ({SourceId} -> {TargetId})", edge.Id, sourceNodeId, targetNodeId);
            return edge;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new KeyNotFoundException(
                $"Source node '{sourceNodeId}' or target node '{targetNodeId}' not found. " +
                "Ensure both nodes exist before creating the edge.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphEdge>> BatchCreateEdgesAsync(
        IEnumerable<(string SourceNodeId, string TargetNodeId, string Label, Dictionary<string, object?>? Properties, double Weight)> edges,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var edgeList = edges.ToList();
        if (edgeList.Count == 0)
            return [];

        var createdEdges = new List<GraphEdge>(edgeList.Count);

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            const string sql = @"
                INSERT INTO {{EDGE_TABLE}} (id, source_node_id, target_node_id, label, properties, weight, created_at)
                VALUES (@id, @source_node_id, @target_node_id, @label, @properties::jsonb, @weight, @created_at)
                RETURNING id, source_node_id, target_node_id, label, properties, weight, created_at";

            var now = DateTime.UtcNow;

            foreach (var (sourceNodeId, targetNodeId, label, properties, weight) in edgeList)
            {
                var edge = new GraphEdge
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SourceNodeId = sourceNodeId,
                    TargetNodeId = targetNodeId,
                    Label = label,
                    Properties = properties ?? new(),
                    Weight = weight,
                    CreatedAt = now
                };

                try
                {
                    await using var command = new NpgsqlCommand(sql.Replace("{{EDGE_TABLE}}", EdgeTable), connection, transaction);
                    command.Parameters.AddWithValue("id", edge.Id);
                    command.Parameters.AddWithValue("source_node_id", edge.SourceNodeId);
                    command.Parameters.AddWithValue("target_node_id", edge.TargetNodeId);
                    command.Parameters.AddWithValue("label", edge.Label);
                    command.Parameters.AddWithValue("properties", JsonSerializer.Serialize(edge.Properties));
                    command.Parameters.AddWithValue("weight", edge.Weight);
                    command.Parameters.AddWithValue("created_at", edge.CreatedAt);

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        createdEdges.Add(ReadEdge(reader));
                    }
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                {
                    throw new KeyNotFoundException(
                        $"Source node '{sourceNodeId}' or target node '{targetNodeId}' not found.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Batch created {Count} edges", createdEdges.Count);
            return createdEdges;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<GraphEdge?> GetEdgeAsync(string edgeId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        const string sql = @"
            SELECT id, source_node_id, target_node_id, label, properties, weight, created_at
            FROM {{EDGE_TABLE}}
            WHERE id = @id";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql.Replace("{{EDGE_TABLE}}", EdgeTable), connection);
        command.Parameters.AddWithValue("id", edgeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadEdge(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEdgeAsync(string edgeId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var sql = $"DELETE FROM {EdgeTable} WHERE id = @id";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", edgeId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Deleted edge {EdgeId}", edgeId);
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphEdge>> GetEdgesByNodeAsync(string nodeId, string? direction = null, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var baseSql = direction?.ToLowerInvariant() switch
        {
            "out" => $"SELECT id, source_node_id, target_node_id, label, properties, weight, created_at FROM {EdgeTable} WHERE source_node_id = @node_id",
            "in" => $"SELECT id, source_node_id, target_node_id, label, properties, weight, created_at FROM {EdgeTable} WHERE target_node_id = @node_id",
            _ => $"SELECT id, source_node_id, target_node_id, label, properties, weight, created_at FROM {EdgeTable} WHERE source_node_id = @node_id OR target_node_id = @node_id"
        };

        var sql = $"{baseSql} ORDER BY created_at OFFSET @offset LIMIT @limit";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("node_id", nodeId);
        command.Parameters.AddWithValue("offset", offset);
        command.Parameters.AddWithValue("limit", limit);

        var edges = new List<GraphEdge>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            edges.Add(ReadEdge(reader));
        }

        return edges;
    }

    /// <inheritdoc />
    public async Task<long> CountEdgesByNodeAsync(string nodeId, string? direction = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var sql = direction?.ToLowerInvariant() switch
        {
            "out" => $"SELECT COUNT(*) FROM {EdgeTable} WHERE source_node_id = @node_id",
            "in" => $"SELECT COUNT(*) FROM {EdgeTable} WHERE target_node_id = @node_id",
            _ => $"SELECT COUNT(*) FROM {EdgeTable} WHERE source_node_id = @node_id OR target_node_id = @node_id"
        };

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("node_id", nodeId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    /// <inheritdoc />
    public async Task<GraphEdge> UpdateEdgeAsync(string edgeId, Dictionary<string, object?>? properties = null, double? weight = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // 构建动态更新语句
        var setClauses = new List<string> { "properties = @properties::jsonb" };
        if (weight.HasValue)
        {
            setClauses.Add("weight = @weight");
        }

        var sql = $@"
            UPDATE {EdgeTable}
            SET {string.Join(", ", setClauses)}
            WHERE id = @id
            RETURNING id, source_node_id, target_node_id, label, properties, weight, created_at";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", edgeId);
        command.Parameters.AddWithValue("properties", JsonSerializer.Serialize(properties ?? new Dictionary<string, object?>()));
        if (weight.HasValue)
        {
            command.Parameters.AddWithValue("weight", weight.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            _logger.LogDebug("Updated edge {EdgeId}", edgeId);
            return ReadEdge(reader);
        }

        throw new KeyNotFoundException($"Edge with id '{edgeId}' not found");
    }

    /// <inheritdoc />
    public async Task<int> BatchDeleteNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var idList = nodeIds.ToList();
        if (idList.Count == 0)
            return 0;

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            // 先删除所有关联的边
            var deleteEdgesSql = $"DELETE FROM {EdgeTable} WHERE source_node_id = ANY(@ids) OR target_node_id = ANY(@ids)";
            await using (var cmd = new NpgsqlCommand(deleteEdgesSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("ids", idList.ToArray());
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 删除节点
            var deleteNodesSql = $"DELETE FROM {NodeTable} WHERE id = ANY(@ids)";
            await using (var cmd = new NpgsqlCommand(deleteNodesSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("ids", idList.ToArray());
                var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogDebug("Batch deleted {Count} nodes", rowsAffected);
                return rowsAffected;
            }
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> BatchDeleteEdgesAsync(IEnumerable<string> edgeIds, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var idList = edgeIds.ToList();
        if (idList.Count == 0)
            return 0;

        var sql = $"DELETE FROM {EdgeTable} WHERE id = ANY(@ids)";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ids", idList.ToArray());

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Batch deleted {Count} edges", rowsAffected);
        return rowsAffected;
    }

    /// <inheritdoc />
    public async Task<GraphStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var stats = new GraphStatistics();

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);

        // 节点总数
        var totalNodesSql = $"SELECT COUNT(*) FROM {NodeTable}";
        await using (var cmd = new NpgsqlCommand(totalNodesSql, connection))
        {
            stats.TotalNodes = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        }

        // 边总数
        var totalEdgesSql = $"SELECT COUNT(*) FROM {EdgeTable}";
        await using (var cmd = new NpgsqlCommand(totalEdgesSql, connection))
        {
            stats.TotalEdges = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        }

        // 节点标签分布
        var nodesByLabelSql = $"SELECT label, COUNT(*) FROM {NodeTable} GROUP BY label";
        await using (var cmd = new NpgsqlCommand(nodesByLabelSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                stats.NodesByLabel[reader.GetString(0)] = reader.GetInt64(1);
            }
        }

        // 边标签分布
        var edgesByLabelSql = $"SELECT label, COUNT(*) FROM {EdgeTable} GROUP BY label";
        await using (var cmd = new NpgsqlCommand(edgesByLabelSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                stats.EdgesByLabel[reader.GetString(0)] = reader.GetInt64(1);
            }
        }

        // 孤立节点（无边关联）
        var orphanSql = $@"
            SELECT COUNT(*) FROM {NodeTable} n
            WHERE NOT EXISTS (SELECT 1 FROM {EdgeTable} e WHERE e.source_node_id = n.id OR e.target_node_id = n.id)";
        await using (var cmd = new NpgsqlCommand(orphanSql, connection))
        {
            stats.OrphanNodes = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        }

        // 平均节点度数
        stats.AverageNodeDegree = stats.TotalNodes > 0
            ? (double)stats.TotalEdges * 2 / stats.TotalNodes
            : 0;

        return stats;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(Func<IGraphEngine, Task<T>> action, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            // 创建事务内的引擎代理（共享连接和事务）
            var transactionEngine = new TransactionalGraphEngineProxy(this, connection, transaction);
            var result = await action(transactionEngine);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> TraverseBfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // 使用递归 CTE 批量获取所有可达节点，避免 N+1 查询
        var edgeFilter = edgeLabel != null ? "AND e.label = @edge_label" : "";

        var cteSql = $@"
            WITH RECURSIVE search AS (
                -- Base case: start node
                SELECT n.id, n.label, n.properties, n.created_at, n.updated_at, 0 AS depth
                FROM {NodeTable} n
                WHERE n.id = @start_node_id

                UNION ALL

                -- Recursive case: neighbors
                SELECT n.id, n.label, n.properties, n.created_at, n.updated_at, s.depth + 1
                FROM search s
                JOIN {EdgeTable} e ON e.source_node_id = s.id
                JOIN {NodeTable} n ON n.id = e.target_node_id
                WHERE s.depth < @max_depth
                  AND n.id NOT IN (SELECT id FROM search)
                  {edgeFilter}
            )
            SELECT id, label, properties, created_at, updated_at
            FROM search
            ORDER BY depth";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(cteSql, connection);
        command.Parameters.AddWithValue("start_node_id", startNodeId);
        command.Parameters.AddWithValue("max_depth", maxDepth);
        if (edgeLabel != null)
        {
            command.Parameters.AddWithValue("edge_label", edgeLabel);
        }

        var nodes = new List<GraphNode>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nodes.Add(ReadNode(reader));
        }

        return nodes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> TraverseDfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // 使用递归 CTE 实现 DFS（通过 depth 排序实现后进先出）
        var edgeFilter = edgeLabel != null ? "AND e.label = @edge_label" : "";

        var cteSql = $@"
            WITH RECURSIVE search AS (
                SELECT n.id, n.label, n.properties, n.created_at, n.updated_at, 0 AS depth,
                       ARRAY[n.id] AS path
                FROM {NodeTable} n
                WHERE n.id = @start_node_id

                UNION ALL

                SELECT n.id, n.label, n.properties, n.created_at, n.updated_at, s.depth + 1,
                       s.path || n.id
                FROM search s
                JOIN {EdgeTable} e ON e.source_node_id = s.id
                JOIN {NodeTable} n ON n.id = e.target_node_id
                WHERE s.depth < @max_depth
                  AND n.id <> ALL(s.path)
                  {edgeFilter}
            )
            SELECT id, label, properties, created_at, updated_at
            FROM search
            ORDER BY depth, path";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(cteSql, connection);
        command.Parameters.AddWithValue("start_node_id", startNodeId);
        command.Parameters.AddWithValue("max_depth", maxDepth);
        if (edgeLabel != null)
        {
            command.Parameters.AddWithValue("edge_label", edgeLabel);
        }

        var nodes = new List<GraphNode>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nodes.Add(ReadNode(reader));
        }

        return nodes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ShortestPathAsync(string startNodeId, string endNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // 使用递归 CTE 实现无权最短路径 (BFS)
        var edgeFilter = edgeLabel != null ? "AND e.label = @edge_label" : "";

        var cteSql = $@"
            WITH RECURSIVE search AS (
                SELECT n.id, ARRAY[n.id] AS path, 0 AS depth
                FROM {NodeTable} n
                WHERE n.id = @start_node_id

                UNION ALL

                SELECT n.id, s.path || n.id, s.depth + 1
                FROM search s
                JOIN {EdgeTable} e ON e.source_node_id = s.id
                JOIN {NodeTable} n ON n.id = e.target_node_id
                WHERE n.id <> ALL(s.path)
                  {edgeFilter}
                  AND s.depth < 1000  -- 安全限制，防止无限循环
            )
            SELECT path
            FROM search
            WHERE id = @end_node_id
            ORDER BY depth
            LIMIT 1";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(cteSql, connection);
        command.Parameters.AddWithValue("start_node_id", startNodeId);
        command.Parameters.AddWithValue("end_node_id", endNodeId);
        if (edgeLabel != null)
        {
            command.Parameters.AddWithValue("edge_label", edgeLabel);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.GetFieldValue<string[]>(0);
        }

        return Array.Empty<string>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string NodeId, double Distance)>> DijkstraAsync(
        string startNodeId,
        string? edgeLabel = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // 使用递归 CTE 实现 Dijkstra 算法（带权最短路径）
        // 注意：对于大规模图，建议使用 pgRouting 扩展
        var edgeFilter = edgeLabel != null ? "AND e.label = @edge_label" : "";

        var cteSql = $@"
            WITH RECURSIVE dijkstra AS (
                SELECT n.id, 0.0 AS distance, ARRAY[n.id] AS path
                FROM {NodeTable} n
                WHERE n.id = @start_node_id

                UNION ALL

                SELECT n.id, d.distance + e.weight, d.path || n.id
                FROM dijkstra d
                JOIN {EdgeTable} e ON e.source_node_id = d.id
                JOIN {NodeTable} n ON n.id = e.target_node_id
                WHERE n.id <> ALL(d.path)
                  {edgeFilter}
            )
            SELECT id, MIN(distance) AS distance
            FROM dijkstra
            GROUP BY id
            ORDER BY distance";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(cteSql, connection);
        command.Parameters.AddWithValue("start_node_id", startNodeId);
        if (edgeLabel != null)
        {
            command.Parameters.AddWithValue("edge_label", edgeLabel);
        }

        var result = new List<(string NodeId, double Distance)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add((reader.GetString(0), reader.GetDouble(1)));
        }

        return result;
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var createSchemaSql = $@"CREATE SCHEMA IF NOT EXISTS ""{_options.GraphName}""";

            var createNodesTableSql = $@"
                CREATE TABLE IF NOT EXISTS {NodeTable} (
                    id VARCHAR(64) PRIMARY KEY,
                    label VARCHAR(255) NOT NULL,
                    properties JSONB DEFAULT '{{}}',
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                )";

            var createEdgesTableSql = $@"
                CREATE TABLE IF NOT EXISTS {EdgeTable} (
                    id VARCHAR(64) PRIMARY KEY,
                    source_node_id VARCHAR(64) NOT NULL,
                    target_node_id VARCHAR(64) NOT NULL,
                    label VARCHAR(255) NOT NULL,
                    properties JSONB DEFAULT '{{}}',
                    weight DOUBLE PRECISION DEFAULT 1.0,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    CONSTRAINT fk_source_node FOREIGN KEY (source_node_id) REFERENCES {NodeTable}(id) ON DELETE CASCADE,
                    CONSTRAINT fk_target_node FOREIGN KEY (target_node_id) REFERENCES {NodeTable}(id) ON DELETE CASCADE
                )";

            await using var cmd1 = new NpgsqlCommand(createSchemaSql, connection, transaction);
            await cmd1.ExecuteNonQueryAsync(cancellationToken);

            await using var cmd2 = new NpgsqlCommand(createNodesTableSql, connection, transaction);
            await cmd2.ExecuteNonQueryAsync(cancellationToken);

            await using var cmd3 = new NpgsqlCommand(createEdgesTableSql, connection, transaction);
            await cmd3.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Created graph schema for '{GraphName}'", _options.GraphName);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        var nodeLabelIndex = $"CREATE INDEX IF NOT EXISTS \"{_options.IndexNamePrefix}_node_label\" ON {NodeTable}(label)";
        var edgeSourceIndex = $"CREATE INDEX IF NOT EXISTS \"{_options.IndexNamePrefix}_edge_source\" ON {EdgeTable}(source_node_id)";
        var edgeTargetIndex = $"CREATE INDEX IF NOT EXISTS \"{_options.IndexNamePrefix}_edge_target\" ON {EdgeTable}(target_node_id)";
        var edgeLabelIndex = $"CREATE INDEX IF NOT EXISTS \"{_options.IndexNamePrefix}_edge_label\" ON {EdgeTable}(label)";
        var edgeWeightIndex = $"CREATE INDEX IF NOT EXISTS \"{_options.IndexNamePrefix}_edge_weight\" ON {EdgeTable}(weight)";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var cmd1 = new NpgsqlCommand(nodeLabelIndex, connection);
        await cmd1.ExecuteNonQueryAsync(cancellationToken);
        await using var cmd2 = new NpgsqlCommand(edgeSourceIndex, connection);
        await cmd2.ExecuteNonQueryAsync(cancellationToken);
        await using var cmd3 = new NpgsqlCommand(edgeTargetIndex, connection);
        await cmd3.ExecuteNonQueryAsync(cancellationToken);
        await using var cmd4 = new NpgsqlCommand(edgeLabelIndex, connection);
        await cmd4.ExecuteNonQueryAsync(cancellationToken);
        await using var cmd5 = new NpgsqlCommand(edgeWeightIndex, connection);
        await cmd5.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Created graph indexes for '{GraphName}'", _options.GraphName);
    }

    private void EnsureInitialized()
    {
        if (_dataSource == null)
        {
            throw new InvalidOperationException("Graph engine has not been initialized. Call InitializeAsync first.");
        }
    }

    private static GraphNode ReadNode(NpgsqlDataReader reader)
    {
        return new GraphNode
        {
            Id = reader.GetString(0),
            Label = reader.GetString(1),
            Properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(reader.GetString(2)) ?? new(),
            CreatedAt = reader.GetDateTime(3),
            UpdatedAt = reader.GetDateTime(4)
        };
    }

    private static GraphEdge ReadEdge(NpgsqlDataReader reader)
    {
        return new GraphEdge
        {
            Id = reader.GetString(0),
            SourceNodeId = reader.GetString(1),
            TargetNodeId = reader.GetString(2),
            Label = reader.GetString(3),
            Properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(reader.GetString(4)) ?? new(),
            Weight = reader.GetDouble(5),
            CreatedAt = reader.GetDateTime(6)
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_dataSource != null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }
    }
}

/// <summary>
/// 事务内图引擎代理 - 共享外部连接和事务
/// </summary>
internal sealed class TransactionalGraphEngineProxy : IGraphEngine
{
    private readonly NpgsqlGraphEngine _inner;
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    public TransactionalGraphEngineProxy(NpgsqlGraphEngine inner, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _inner = inner;
        _connection = connection;
        _transaction = transaction;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<GraphNode> CreateNodeAsync(string label, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
        => _inner.CreateNodeAsync(label, properties, cancellationToken);

    public Task<GraphNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        => _inner.GetNodeAsync(nodeId, cancellationToken);

    public Task<GraphNode> UpdateNodeAsync(string nodeId, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
        => _inner.UpdateNodeAsync(nodeId, properties, cancellationToken);

    public Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        => _inner.DeleteNodeAsync(nodeId, cancellationToken);

    public Task<IReadOnlyList<GraphNode>> GetNodesByLabelAsync(string label, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
        => _inner.GetNodesByLabelAsync(label, offset, limit, cancellationToken);

    public Task<long> CountNodesByLabelAsync(string label, CancellationToken cancellationToken = default)
        => _inner.CountNodesByLabelAsync(label, cancellationToken);

    public Task<GraphEdge> CreateEdgeAsync(string sourceNodeId, string targetNodeId, string label, Dictionary<string, object?>? properties = null, double weight = 1.0, CancellationToken cancellationToken = default)
        => _inner.CreateEdgeAsync(sourceNodeId, targetNodeId, label, properties, weight, cancellationToken);

    public Task<GraphEdge?> GetEdgeAsync(string edgeId, CancellationToken cancellationToken = default)
        => _inner.GetEdgeAsync(edgeId, cancellationToken);

    public Task<GraphEdge> UpdateEdgeAsync(string edgeId, Dictionary<string, object?>? properties = null, double? weight = null, CancellationToken cancellationToken = default)
        => _inner.UpdateEdgeAsync(edgeId, properties, weight, cancellationToken);

    public Task<bool> DeleteEdgeAsync(string edgeId, CancellationToken cancellationToken = default)
        => _inner.DeleteEdgeAsync(edgeId, cancellationToken);

    public Task<IReadOnlyList<GraphEdge>> GetEdgesByNodeAsync(string nodeId, string? direction = null, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
        => _inner.GetEdgesByNodeAsync(nodeId, direction, offset, limit, cancellationToken);

    public Task<long> CountEdgesByNodeAsync(string nodeId, string? direction = null, CancellationToken cancellationToken = default)
        => _inner.CountEdgesByNodeAsync(nodeId, direction, cancellationToken);

    public Task<IReadOnlyList<GraphNode>> TraverseBfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
        => _inner.TraverseBfsAsync(startNodeId, edgeLabel, maxDepth, cancellationToken);

    public Task<IReadOnlyList<GraphNode>> TraverseDfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
        => _inner.TraverseDfsAsync(startNodeId, edgeLabel, maxDepth, cancellationToken);

    public Task<IReadOnlyList<string>> ShortestPathAsync(string startNodeId, string endNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default)
        => _inner.ShortestPathAsync(startNodeId, endNodeId, edgeLabel, cancellationToken);

    public Task<IReadOnlyList<GraphNode>> BatchCreateNodesAsync(IEnumerable<(string Label, Dictionary<string, object?>? Properties)> nodes, CancellationToken cancellationToken = default)
        => _inner.BatchCreateNodesAsync(nodes, cancellationToken);

    public Task<IReadOnlyList<GraphNode>> BatchGetNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
        => _inner.BatchGetNodesAsync(nodeIds, cancellationToken);

    public Task<IReadOnlyList<GraphEdge>> BatchCreateEdgesAsync(IEnumerable<(string SourceNodeId, string TargetNodeId, string Label, Dictionary<string, object?>? Properties, double Weight)> edges, CancellationToken cancellationToken = default)
        => _inner.BatchCreateEdgesAsync(edges, cancellationToken);

    public Task<int> BatchDeleteNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
        => _inner.BatchDeleteNodesAsync(nodeIds, cancellationToken);

    public Task<int> BatchDeleteEdgesAsync(IEnumerable<string> edgeIds, CancellationToken cancellationToken = default)
        => _inner.BatchDeleteEdgesAsync(edgeIds, cancellationToken);

    public Task<IReadOnlyList<(string NodeId, double Distance)>> DijkstraAsync(string startNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default)
        => _inner.DijkstraAsync(startNodeId, edgeLabel, cancellationToken);

    public Task<GraphStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        => _inner.GetStatisticsAsync(cancellationToken);

    public Task<T> ExecuteInTransactionAsync<T>(Func<IGraphEngine, Task<T>> action, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Nested transactions are not supported.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
