using Npgsql;

namespace QiaKon.Retrieval.VectorStore.Npgsql;

/// <summary>
/// PostgreSQL + pgvector 向量存储实现
/// </summary>
public sealed class NpgsqlVectorStore : IVectorStore, IAsyncDisposable
{
    private readonly NpgsqlVectorStoreOptions _options;
    private NpgsqlDataSource? _dataSource;
    private readonly Dictionary<string, NpgsqlVectorCollection> _collections = new();
    private bool _disposed;

    public NpgsqlVectorStore(NpgsqlVectorStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("ConnectionString cannot be null or empty", nameof(options));
        }
    }

    /// <summary>
    /// 初始化数据源源
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var builder = new NpgsqlDataSourceBuilder(_options.ConnectionString)
        {
            ConnectionStringBuilder =
            {
                MaxPoolSize = _options.MaxPoolSize,
                CommandTimeout = _options.CommandTimeoutSeconds
            }
        };

        // 启用 pgvector 类型支持
        builder.UseVector();

        _dataSource = builder.Build();

        // 测试连接
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IVectorCollection> GetOrCreateCollectionAsync(
        string name,
        int dimensions,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        if (_collections.TryGetValue(name, out var existing))
        {
            if (existing.Dimensions != dimensions)
            {
                throw new InvalidOperationException(
                    $"Collection '{name}' already exists with dimensions {existing.Dimensions}, but {dimensions} was requested.");
            }
            return existing;
        }

        var collection = new NpgsqlVectorCollection(_dataSource!, _options, name, dimensions);
        await collection.EnsureExistsAsync(cancellationToken);

        _collections[name] = collection;
        return collection;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var tableName = $"\"{_options.Schema}\".\"vs_{name}\"";
        var sql = $@"DROP TABLE IF EXISTS {tableName};";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _collections.Remove(name);

        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureInitialized();

        var sql = $@"
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = @schema
              AND table_name LIKE 'vs_%'
              AND table_type = 'BASE TABLE';";

        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", _options.Schema);

        var collections = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var tableName = reader.GetString(0);
            if (tableName.StartsWith("vs_"))
            {
                collections.Add(tableName[3..]);
            }
        }

        return collections;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        foreach (var collection in _collections.Values)
        {
            await collection.DisposeAsync();
        }

        _collections.Clear();
        _dataSource?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NpgsqlVectorStore));
    }

    private void EnsureInitialized()
    {
        if (_dataSource == null)
            throw new InvalidOperationException("NpgsqlVectorStore is not initialized. Call InitializeAsync first.");
    }
}
