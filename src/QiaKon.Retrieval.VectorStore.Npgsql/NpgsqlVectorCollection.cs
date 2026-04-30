using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace QiaKon.Retrieval.VectorStore.Npgsql;

/// <summary>
/// PostgreSQL 向量集合实现。
/// 优先使用 pgvector；若数据库未安装 vector 扩展，则自动回退到 REAL[] 存储并在 .NET 侧计算相似度。
/// </summary>
public sealed class NpgsqlVectorCollection : IVectorCollection
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly NpgsqlVectorStoreOptions _options;
    private readonly string _name;
    private readonly int _dimensions;
    private bool _disposed;
    private bool _initialized;
    private bool _useArrayFallback;

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

    public async Task EnsureExistsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            if (_options.AutoCreateExtension)
            {
                await using var createExtensionCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", connection);
                await createExtensionCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await CreateVectorTableAsync(connection, cancellationToken);
            await CreateIndexAsync(connection, cancellationToken);
        }
        catch (PostgresException ex) when (IsVectorExtensionUnavailable(ex))
        {
            _useArrayFallback = true;
            await CreateArrayTableAsync(connection, cancellationToken);
        }

        _initialized = true;
    }

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
        AddEmbeddingParameter(cmd, "embedding", record.Embedding);
        cmd.Parameters.AddWithValue("text", record.Text ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("metadata", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(record.Metadata));
        cmd.Parameters.AddWithValue("createdAt", record.CreatedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertBatchAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var recordList = records as IReadOnlyList<VectorRecord> ?? records.ToList();
        if (recordList.Count == 0)
        {
            return;
        }

        var tableName = GetTableName();
        var batchSize = _options.BatchSize;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        for (var i = 0; i < recordList.Count; i += batchSize)
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
                    AddEmbeddingParameter(cmd, "embedding", record.Embedding);
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

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        VectorSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        options ??= new VectorSearchOptions();

        return _useArrayFallback
            ? await SearchWithArrayFallbackAsync(embedding, options, cancellationToken)
            : await SearchWithVectorExtensionAsync(embedding, options, cancellationToken);
    }

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

    public async Task DeleteBatchAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var idList = ids as IReadOnlyList<Guid> ?? ids.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        var tableName = GetTableName();
        var sql = $@"DELETE FROM {tableName} WHERE id = ANY(@ids);";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("ids", idList.ToArray());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

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

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private async Task<IReadOnlyList<VectorSearchResult>> SearchWithVectorExtensionAsync(
        ReadOnlyMemory<float> embedding,
        VectorSearchOptions options,
        CancellationToken cancellationToken)
    {
        var tableName = GetTableName();
        var (distanceOp, orderDirection) = GetDistanceOperator(options.DistanceMetric);
        var scoreExpr = GetScoreExpression(options.DistanceMetric, distanceOp);

        var sql = $@"
            SELECT id, embedding, text_content, metadata, created_at, {scoreExpr} as score
            FROM {tableName}
            WHERE 1=1
            {(options.MinSimilarity.HasValue ? $"AND {scoreExpr} >= @minSimilarity" : string.Empty)}
            {(string.IsNullOrWhiteSpace(options.Filter) ? string.Empty : $"AND ({options.Filter})")}
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

        if (options.FilterParameters is not null)
        {
            foreach (var parameter in options.FilterParameters)
            {
                cmd.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
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

    private async Task<IReadOnlyList<VectorSearchResult>> SearchWithArrayFallbackAsync(
        ReadOnlyMemory<float> embedding,
        VectorSearchOptions options,
        CancellationToken cancellationToken)
    {
        var tableName = GetTableName();
        var sql = $@"
            SELECT id, embedding, text_content, metadata, created_at
            FROM {tableName};";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;

        var results = new List<VectorSearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = ReadRecord(reader);
            var (score, distance) = CalculateSimilarity(record.Embedding.Span, embedding.Span, options.DistanceMetric);
            if (options.MinSimilarity.HasValue && score < options.MinSimilarity.Value)
            {
                continue;
            }

            results.Add(new VectorSearchResult
            {
                Record = record,
                Score = score,
                Distance = distance,
            });
        }

        return results
            .OrderByDescending(result => result.Score)
            .Take(options.TopK)
            .ToList();
    }

    private async Task CreateVectorTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var tableName = GetTableName();
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id UUID PRIMARY KEY,
                embedding VECTOR({_dimensions}),
                text_content TEXT,
                metadata JSONB DEFAULT '{{}}',
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CreateArrayTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var tableName = GetTableName();
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                id UUID PRIMARY KEY,
                embedding REAL[],
                text_content TEXT,
                metadata JSONB DEFAULT '{{}}',
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private string GetTableName() => $"\"{_options.Schema}\".\"vs_{_name}\"";

    private async Task CreateIndexAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        if (_useArrayFallback || _options.IndexType == VectorIndexType.None)
        {
            return;
        }

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

    private void AddEmbeddingParameter(NpgsqlCommand cmd, string parameterName, ReadOnlyMemory<float> embedding)
    {
        if (_useArrayFallback)
        {
            cmd.Parameters.AddWithValue(parameterName, NpgsqlDbType.Array | NpgsqlDbType.Real, embedding.ToArray());
            return;
        }

        cmd.Parameters.AddWithValue(parameterName, new Vector(embedding.ToArray()));
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

    private VectorRecord ReadRecord(NpgsqlDataReader reader)
    {
        var embedding = _useArrayFallback
            ? new ReadOnlyMemory<float>(reader.GetFieldValue<float[]>(reader.GetOrdinal("embedding")))
            : new ReadOnlyMemory<float>(reader.GetFieldValue<Vector>(reader.GetOrdinal("embedding")).ToArray());
        var metadataJson = reader.GetString(reader.GetOrdinal("metadata"));

        return new VectorRecord
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Embedding = embedding,
            Text = reader.IsDBNull(reader.GetOrdinal("text_content"))
                ? null
                : reader.GetString(reader.GetOrdinal("text_content")),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, object?>>(metadataJson)
                      ?? new Dictionary<string, object?>(),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }

    private VectorSearchResult ReadSearchResult(NpgsqlDataReader reader)
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

    private static (float Score, float Distance) CalculateSimilarity(
        ReadOnlySpan<float> left,
        ReadOnlySpan<float> right,
        DistanceMetric metric)
    {
        var length = Math.Min(left.Length, right.Length);
        if (length == 0)
        {
            return (0f, 1f);
        }

        return metric switch
        {
            DistanceMetric.Euclidean => CalculateEuclidean(left[..length], right[..length]),
            DistanceMetric.InnerProduct => CalculateInnerProduct(left[..length], right[..length]),
            _ => CalculateCosine(left[..length], right[..length])
        };
    }

    private static (float Score, float Distance) CalculateEuclidean(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        var sum = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            var delta = left[i] - right[i];
            sum += delta * delta;
        }

        var distance = (float)Math.Sqrt(sum);
        return (1f / (1f + distance), distance);
    }

    private static (float Score, float Distance) CalculateInnerProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        var dot = 0f;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
        }

        return (dot, -dot);
    }

    private static (float Score, float Distance) CalculateCosine(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        var dot = 0d;
        var leftNorm = 0d;
        var rightNorm = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm <= 0d || rightNorm <= 0d)
        {
            return (0f, 1f);
        }

        var cosine = dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
        var score = (float)((cosine + 1d) / 2d);
        return (score, 1f - score);
    }

    private static bool IsVectorExtensionUnavailable(PostgresException ex)
        => ex.SqlState == "0A000"
            && ex.MessageText.Contains("extension \"vector\" is not available", StringComparison.OrdinalIgnoreCase);

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
