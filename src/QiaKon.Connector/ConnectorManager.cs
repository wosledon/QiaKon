using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Connector;

/// <summary>
/// 连接器管理器（配置驱动）
/// </summary>
public sealed class ConnectorManager : IConnectorManager, IDisposable, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, IConnectorOptions> _options;
    private readonly Dictionary<string, IConnector> _connectors = new();
    private bool _initialized;
    private bool _disposed;

    public ConnectorManager(
        IServiceProvider serviceProvider,
        IReadOnlyDictionary<string, IConnectorOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public void Register(IConnector connector)
    {
        if (connector == null)
            throw new ArgumentNullException(nameof(connector));

        if (_connectors.ContainsKey(connector.Name))
        {
            throw new InvalidOperationException($"Connector '{connector.Name}' is already registered");
        }

        _connectors[connector.Name] = connector;
    }

    /// <inheritdoc />
    public IConnector? Get(string name)
    {
        return _connectors.TryGetValue(name, out var connector) ? connector : null;
    }

    /// <inheritdoc />
    public async Task InitializeAllAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        foreach (var (name, options) in _options)
        {
            var factory = _serviceProvider.GetRequiredService<IConnectorFactory>();
            var connector = factory.Create(options);
            _connectors[name] = connector;

            await connector.InitializeAsync(cancellationToken);
        }

        _initialized = true;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, HealthCheckResult>> HealthCheckAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HealthCheckResult>();

        foreach (var (name, connector) in _connectors)
        {
            try
            {
                results[name] = await connector.HealthCheckAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                results[name] = new HealthCheckResult(
                    IsHealthy: false,
                    Message: ex.Message,
                    Latency: null);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task CloseAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (_, connector) in _connectors)
        {
            await connector.CloseAsync(cancellationToken);
        }

        _connectors.Clear();
        _initialized = false;
    }

    /// <summary>
    /// 获取指定名称的连接器（强类型）
    /// </summary>
    public T GetConnector<T>(string name) where T : IConnector
    {
        if (!_connectors.TryGetValue(name, out var connector))
        {
            throw new KeyNotFoundException($"Connector '{name}' not found");
        }

        if (connector is not T typedConnector)
        {
            throw new InvalidCastException(
                $"Connector '{name}' is of type '{connector.GetType().Name}', not '{typeof(T).Name}'");
        }

        return typedConnector;
    }

    /// <summary>
    /// 获取指定名称的连接器（返回 IConnector）
    /// </summary>
    public IConnector GetConnector(string name)
    {
        return GetConnector<IConnector>(name);
    }

    /// <summary>
    /// 获取所有连接器
    /// </summary>
    public IReadOnlyDictionary<string, IConnector> GetAllConnectors()
    {
        return _connectors;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await CloseAllAsync(CancellationToken.None);
        _disposed = true;
    }

    public void Dispose()
    {
        CloseAllAsync(CancellationToken.None).GetAwaiter().GetResult();
        _disposed = true;
    }
}
