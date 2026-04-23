namespace QiaKon.Connector.Npgsql;

/// <summary>
/// PostgreSQL 连接器工厂
/// </summary>
public sealed class NpgsqlConnectorFactory : IConnectorFactory
{
    public IConnector Create(IConnectorOptions options)
    {
        if (options is not NpgsqlConnectorOptions npgsqlOptions)
        {
            throw new ArgumentException(
                $"Expected NpgsqlConnectorOptions, got {options.GetType().Name}",
                nameof(options));
        }

        return new NpgsqlConnector(npgsqlOptions);
    }
}
