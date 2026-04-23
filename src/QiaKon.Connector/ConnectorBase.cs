using QiaKon.Connector;

namespace QiaKon.Connector;

/// <summary>
/// 连接器抽象基类，提供通用实现
/// </summary>
public abstract class ConnectorBase : IConnector
{
    private ConnectorState _state = ConnectorState.NotInitialized;
    private bool _disposed;

    protected ConnectorBase(string name, ConnectorType type)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public ConnectorType Type { get; }

    /// <inheritdoc />
    public ConnectorState State
    {
        get => _state;
        protected set => _state = value;
    }

    /// <inheritdoc />
    public abstract Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带重试的操作
    /// </summary>
    protected async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int retryCount,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        var lastException = default(Exception);

        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < retryCount)
            {
                lastException = ex;

                if (attempt > 0)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
        }

        throw new ConnectorException(
            $"Operation failed after {retryCount + 1} attempts",
            lastException ?? new Exception("Unknown error"));
    }

    /// <summary>
    /// 验证连接状态
    /// </summary>
    protected void EnsureConnected()
    {
        if (State != ConnectorState.Connected)
        {
            throw new InvalidOperationException(
                $"Connector '{Name}' is not connected. Current state: {State}");
        }
    }

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}

/// <summary>
/// 连接器异常
/// </summary>
public sealed class ConnectorException : Exception
{
    public ConnectorException(string message) : base(message) { }

    public ConnectorException(string message, Exception innerException)
        : base(message, innerException) { }
}
