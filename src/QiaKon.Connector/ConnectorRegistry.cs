using System.Collections.Concurrent;

namespace QiaKon.Connector;

/// <summary>
/// 连接器注册表默认实现
/// </summary>
public sealed class ConnectorRegistry : IConnectorRegistry, IDisposable
{
    private readonly ConcurrentDictionary<string, IConnector> _connectors = new();
    private bool _disposed;

    /// <inheritdoc />
    public IConnector Get(string name)
    {
        if (TryGet(name, out var connector))
        {
            return connector ?? throw new KeyNotFoundException($"Connector '{name}' not found");
        }

        throw new KeyNotFoundException($"Connector '{name}' not found");
    }

    /// <inheritdoc />
    public bool TryGet(string name, out IConnector? connector)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            connector = null;
            return false;
        }

        return _connectors.TryGetValue(name, out connector);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllNames()
    {
        return _connectors.Keys;
    }

    /// <summary>
    /// 注册连接器
    /// </summary>
    internal void Register(string name, IConnector connector)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connector name cannot be null or empty", nameof(name));

        if (connector == null)
            throw new ArgumentNullException(nameof(connector));

        if (!_connectors.TryAdd(name, connector))
        {
            throw new InvalidOperationException($"Connector '{name}' is already registered");
        }
    }

    /// <summary>
    /// 移除连接器
    /// </summary>
    internal bool Unregister(string name)
    {
        return _connectors.TryRemove(name, out _);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var connector in _connectors.Values)
        {
            connector.Dispose();
        }

        _connectors.Clear();
        _disposed = true;
    }
}
