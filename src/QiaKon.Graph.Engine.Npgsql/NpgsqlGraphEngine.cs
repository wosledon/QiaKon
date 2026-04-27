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

        var sql = $@"
            INSERT INTO {NodeTable} (id, label, properties, created_at, updated_at)
            VALUES (@id, @label, @properties::jsonb, @created_at, @updated_at)
            RETURNING id, label, properties, created_at, updated_at";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
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
    public async Task<GraphNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var sql = $@"
            SELECT id, label, properties, created_at, updated_at
            FROM {NodeTable}
            WHERE id = @id";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", nodeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadNode(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<GraphNode> UpdateNodeAsync(string nodeId, Dictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var updatedAt = DateTime.UtcNow;
        var sql = $@"
            UPDATE {NodeTable}
            SET properties = @properties::jsonb, updated_at = @updated_at
            WHERE id = @id
            RETURNING id, label, properties, created_at, updated_at";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
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

        // 删除节点及其所有边
        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 先删除所有关联的边
            var deleteEdgesSql = $@"
                DELETE FROM {EdgeTable}
                WHERE source_node_id = @id OR target_node_id = @id";

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
    public async Task<IReadOnlyList<GraphNode>> GetNodesByLabelAsync(string label, int limit = 100, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var sql = $@"
            SELECT id, label, properties, created_at, updated_at
            FROM {NodeTable}
            WHERE label = @label
            LIMIT @limit";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("label", label);
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
    public async Task<GraphEdge> CreateEdgeAsync(string sourceNodeId, string targetNodeId, string label, Dictionary<string, object?>? properties = null, double weight = 1.0, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // 验证源节点和目标节点存在
        var sourceNode = await GetNodeAsync(sourceNodeId, cancellationToken);
        var targetNode = await GetNodeAsync(targetNodeId, cancellationToken);

        if (sourceNode == null)
            throw new KeyNotFoundException($"Source node with id '{sourceNodeId}' not found");
        if (targetNode == null)
            throw new KeyNotFoundException($"Target node with id '{targetNodeId}' not found");

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

        var sql = $@"
            INSERT INTO {EdgeTable} (id, source_node_id, target_node_id, label, properties, weight, created_at)
            VALUES (@id, @source_node_id, @target_node_id, @label, @properties::jsonb, @weight, @created_at)
            RETURNING id, source_node_id, target_node_id, label, properties, weight, created_at";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
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

    /// <inheritdoc />
    public async Task<GraphEdge?> GetEdgeAsync(string edgeId, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var sql = $@"
            SELECT id, source_node_id, target_node_id, label, properties, weight, created_at
            FROM {EdgeTable}
            WHERE id = @id";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
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
    public async Task<IReadOnlyList<GraphEdge>> GetEdgesByNodeAsync(string nodeId, string? direction = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var sql = direction?.ToLowerInvariant() switch
        {
            "out" => $"SELECT id, source_node_id, target_node_id, label, properties, weight, created_at FROM {EdgeTable} WHERE source_node_id = @node_id",
            "in" => $"SELECT id, source_node_id, target_node_id, label, properties, weight, created_at FROM {EdgeTable} WHERE target_node_id = @node_id",
            _ => $"SELECT id, source_node_id, target_node_id, label, properties, weight, created_at FROM {EdgeTable} WHERE source_node_id = @node_id OR target_node_id = @node_id"
        };

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("node_id", nodeId);

        var edges = new List<GraphEdge>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            edges.Add(ReadEdge(reader));
        }

        return edges;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> TraverseBfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var result = new List<GraphNode>();
        var visited = new HashSet<string> { startNodeId };
        var queue = new Queue<(string NodeId, int Depth)>();
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0)
        {
            var (currentNodeId, depth) = queue.Dequeue();

            if (depth >= maxDepth)
                continue;

            var node = await GetNodeAsync(currentNodeId, cancellationToken);
            if (node != null)
            {
                result.Add(node);
            }

            // 获取邻居节点
            var edgeSql = edgeLabel != null
                ? $"SELECT target_node_id FROM {EdgeTable} WHERE source_node_id = @node_id AND label = @label"
                : $"SELECT target_node_id FROM {EdgeTable} WHERE source_node_id = @node_id";

            await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(edgeSql, connection);
            command.Parameters.AddWithValue("node_id", currentNodeId);
            if (edgeLabel != null)
            {
                command.Parameters.AddWithValue("label", edgeLabel);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var neighborId = reader.GetString(0);
                if (visited.Add(neighborId))
                {
                    queue.Enqueue((neighborId, depth + 1));
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> TraverseDfsAsync(string startNodeId, string? edgeLabel = null, int maxDepth = 10, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var result = new List<GraphNode>();
        var visited = new HashSet<string>();

        await TraverseDfsRecursiveAsync(startNodeId, edgeLabel, maxDepth, 0, visited, result, cancellationToken);

        return result;
    }

    private async Task TraverseDfsRecursiveAsync(string nodeId, string? edgeLabel, int maxDepth, int currentDepth, HashSet<string> visited, List<GraphNode> result, CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth || visited.Contains(nodeId))
            return;

        visited.Add(nodeId);

        var node = await GetNodeAsync(nodeId, cancellationToken);
        if (node != null)
        {
            result.Add(node);
        }

        var edgeSql = edgeLabel != null
            ? $"SELECT target_node_id FROM {EdgeTable} WHERE source_node_id = @node_id AND label = @label"
            : $"SELECT target_node_id FROM {EdgeTable} WHERE source_node_id = @node_id";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(edgeSql, connection);
        command.Parameters.AddWithValue("node_id", nodeId);
        if (edgeLabel != null)
        {
            command.Parameters.AddWithValue("label", edgeLabel);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var neighborId = reader.GetString(0);
            await TraverseDfsRecursiveAsync(neighborId, edgeLabel, maxDepth, currentDepth + 1, visited, result, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ShortestPathAsync(string startNodeId, string endNodeId, string? edgeLabel = null, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        // 使用 BFS 找最短路径
        var visited = new HashSet<string> { startNodeId };
        var queue = new Queue<List<string>>();
        queue.Enqueue(new List<string> { startNodeId });

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var currentNodeId = path[^1];

            if (currentNodeId == endNodeId)
            {
                return path;
            }

            var edgeSql = edgeLabel != null
                ? $"SELECT target_node_id FROM {EdgeTable} WHERE source_node_id = @node_id AND label = @label"
                : $"SELECT target_node_id FROM {EdgeTable} WHERE source_node_id = @node_id";

            await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(edgeSql, connection);
            command.Parameters.AddWithValue("node_id", currentNodeId);
            if (edgeLabel != null)
            {
                command.Parameters.AddWithValue("label", edgeLabel);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var neighborId = reader.GetString(0);
                if (visited.Add(neighborId))
                {
                    var newPath = new List<string>(path) { neighborId };
                    queue.Enqueue(newPath);
                }
            }
        }

        return Array.Empty<string>();
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken)
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

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var cmd1 = new NpgsqlCommand(createSchemaSql, connection);
        await cmd1.ExecuteNonQueryAsync(cancellationToken);

        await using var cmd2 = new NpgsqlCommand(createNodesTableSql, connection);
        await cmd2.ExecuteNonQueryAsync(cancellationToken);

        await using var cmd3 = new NpgsqlCommand(createEdgesTableSql, connection);
        await cmd3.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Created graph schema for '{GraphName}'", _options.GraphName);
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

        if (_dataSource != null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }

        _disposed = true;
    }
}
