using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace QiaKon.Retrieval.VectorStore.Npgsql;

/// <summary>
/// PostgreSQL + pgvector 向量集合实现
/// </summary>
public sealed class NpgsqlVectorCollection : IVectorCollection
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly NpgsqlVectorStoreOptions _options;
    private readonly string _name;
    private readonly int _dimensions;
    private bool _disposed;
    private bool _initialized;

    public string Name => _name;
    public int Dimensions => _dimensions;

    public NpgsqlVectorCollection(
        NpgsqlDataSource dataSource,
        NpgsqlVectorStoreOptions options,
        string name,
        int dimensions)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _dimensions = dimensions > 0 ? dimensions : throw new ArgumentException("Dimensions must be greater than 0", nameof(dimensions));
    }

    /// <inheritdoc />
    public async Task EnsureExistsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized) return;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 创建 pgvector 扩展
        if (_options.AutoCreateExtension)
        {
            await using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // 创建集合表
        var tableName = GetTableName();
        var createTableSql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id UUID PRIMARY KEY,
                embedding VECTOR({_dimensions}),
                text_content TEXT,
                metadata JSONB DEFAULT '{{}}',
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
            );";

        await using var createCmd = new NpgsqlCommand(createTableSql, connection);
        await createCmd.ExecuteNonQueryAsync(cancellationToken);

        // 创建向量索引
        await CreateIndexAsync(connection, cancellationToken);

        _initialized = true;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(VectorRecord record, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var tableName = GetTableName();
        var sql = $@"
            INSERT INTO {tableName} (id, embedding, text_content, metadata, created_at)
            VALUES (@id, @embedding, @text, @metadata, @createdAt)
            ON CONFLICT (id) DO UPDATE SET
                embedding = EXCLUDED.embedding,
                text_content = EXCLUDED.text_content,
                metadata = EXCLUDED.metadata;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;

        cmd.Parameters.AddWithValue("id", record.Id);
        cmd.Parameters.AddWithValue("embedding", new Vector(record.Embedding.ToArray()));
        cmd.Parameters.AddWithValue("text", record.Text ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("metadata", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(record.Metadata));
        cmd.Parameters.AddWithValue("createdAt", record.CreatedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertBatchAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var recordList = records as IReadOnlyList<VectorRecord> ?? records.ToList();
        if (recordList.Count == 0) return;

        var tableName = GetTableName();
        var batchSize = _options.BatchSize;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        for (int i = 0; i < recordList.Count; i += batchSize)
        {
            var batch = recordList.Skip(i).Take(batchSize).ToList();
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var record in batch)
                {
                    await using var cmd = new NpgsqlCommand($@"
                        INSERT INTO {tableName} (id, embedding, text_content, metadata, created_at)
                        VALUES (@id, @embedding, @text, @metadata, @createdAt)
                        ON CONFLICT (id) DO UPDATE SET
                            embedding = EXCLUDED.embedding,
                            text_content = EXCLUDED.text_content,
                            metadata = EXCLUDED.metadata;", connection, transaction);

                    cmd.CommandTimeout = _options.CommandTimeoutSeconds;
                    cmd.Parameters.AddWithValue("id", record.Id);
                    cmd.Parameters.AddWithValue("embedding", new Vector(record.Embedding.ToArray()));
                    cmd.Parameters.AddWithValue("text", record.Text ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("metadata", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(record.Metadata));
                    cmd.Parameters.AddWithValue("createdAt", record.CreatedAt);

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        VectorSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        options ??= new VectorSearchOptions();

        var tableName = GetTableName();
        var (distanceOp, orderDirection) = GetDistanceOperator(options.DistanceMetric);
        var scoreExpr = GetScoreExpression(options.DistanceMetric, distanceOp);

        var sql = $@"
            SELECT id, embedding, text_content, metadata, created_at, {scoreExpr} as score
            FROM {tableName}
            WHERE 1=1
            {(options.MinSimilarity.HasValue ? $"AND {GetScoreExpression(options.DistanceMetric, distanceOp)} >= @minSimilarity" : "")}
            {(string.IsNullOrWhiteSpace(options.Filter) ? "" : $"AND ({options.Filter})")}
            ORDER BY embedding {distanceOp} @queryEmbedding {orderDirection}
            LIMIT @topK;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;

        cmd.Parameters.AddWithValue("queryEmbedding", new Vector(embedding.ToArray()));
        cmd.Parameters.AddWithValue("topK", options.TopK);

        if (options.MinSimilarity.HasValue)
        {
            cmd.Parameters.AddWithValue("minSimilarity", options.MinSimilarity.Value);
        }

        if (options.FilterParameters != null)
        {
            foreach (var param in options.FilterParameters)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
        }

        var results = new List<VectorSearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadSearchResult(reader));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<VectorRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var tableName = GetTableName();
        var sql = $@"
            SELECT id, embedding, text_content, metadata, created_at
            FROM {tableName}
            WHERE id = @id
            LIMIT 1;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadRecord(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var tableName = GetTableName();
        var sql = $@"DELETE FROM {tableName} WHERE id = @id;";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var idList = ids as IReadOnlyList<Guid> ?? ids.ToList();
        if (idList.Count == 0) return;

        var tableName = GetTableName();
        var sql = $@"DELETE FROM {tableName} WHERE id = ANY(@ids);";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("ids", idList.ToArray());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var tableName = GetTableName();
        var sql = $@"SELECT COUNT(*) FROM {tableName};";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long count ? count : 0;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private string GetTableName() => $"\"{_options.Schema}\".\"vs_{_name}\"";

    private async Task CreateIndexAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        if (_options.IndexType == VectorIndexType.None) return;

        var tableName = GetTableName();
        var indexName = $"idx_{_name}_embedding";
        var indexSql = _options.IndexType switch
        {
            VectorIndexType.Hnsw => $@"
                CREATE INDEX IF NOT EXISTS {indexName}
                ON {tableName}
                USING hnsw (embedding vector_cosine_ops)
                WITH (m = {_options.HnswM}, ef_construction = {_options.HnswEfConstruction});",
            VectorIndexType.Ivfflat => $@"
                CREATE INDEX IF NOT EXISTS {indexName}
                ON {tableName}
                USING ivfflat (embedding vector_cosine_ops);",
            _ => throw new NotSupportedException($"Index type {_options.IndexType} is not supported.")
        };

        await using var cmd = new NpgsqlCommand(indexSql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (string op, string order) GetDistanceOperator(DistanceMetric metric) => metric switch
    {
        DistanceMetric.Euclidean => ("\u003c-\u003e", "ASC"),
        DistanceMetric.CosineDistance => ("\u003c=\u003e", "ASC"),
        DistanceMetric.InnerProduct => ("\u003c#\u003e", "ASC"),
        _ => throw new NotSupportedException($"Distance metric {metric} is not supported.")
    };

    private static string GetScoreExpression(DistanceMetric metric, string distanceOp) => metric switch
    {
        DistanceMetric.Euclidean => $"1.0 / (1.0 + embedding {distanceOp} @queryEmbedding)",
        DistanceMetric.CosineDistance => $"1.0 - (embedding {distanceOp} @queryEmbedding) / 2.0",
        DistanceMetric.InnerProduct => $"-(embedding {distanceOp} @queryEmbedding)",
        _ => throw new NotSupportedException($"Distance metric {metric} is not supported.")
    };

    private static VectorRecord ReadRecord(NpgsqlDataReader reader)
    {
        var embeddingVector = reader.GetFieldValue<Vector>(reader.GetOrdinal("embedding"));
        var metadataJson = reader.GetString(reader.GetOrdinal("metadata"));

        return new VectorRecord
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Embedding = embeddingVector.ToArray(),
            Text = reader.IsDBNull(reader.GetOrdinal("text_content"))
                ? null
                : reader.GetString(reader.GetOrdinal("text_content")),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(metadataJson)
                      ?? new Dictionary<string, object?>(),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }

    private static VectorSearchResult ReadSearchResult(NpgsqlDataReader reader)
    {
        var record = ReadRecord(reader);
        var score = reader.GetFloat(reader.GetOrdinal("score"));

        return new VectorSearchResult
        {
            Record = record,
            Score = score,
            Distance = 1.0f - score
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NpgsqlVectorCollection));
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Collection is not initialized. Call EnsureExistsAsync first.");
    }
}
