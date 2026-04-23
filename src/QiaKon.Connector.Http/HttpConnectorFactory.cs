namespace QiaKon.Connector.Http;

/// <summary>
/// HTTP 连接器工厂
/// </summary>
public sealed class HttpConnectorFactory : IConnectorFactory
{
    public IConnector Create(IConnectorOptions options)
    {
        if (options is not HttpConnectorOptions httpOptions)
        {
            throw new ArgumentException(
                $"Expected HttpConnectorOptions, got {options.GetType().Name}",
                nameof(options));
        }

        return new HttpConnector(httpOptions);
    }
}

