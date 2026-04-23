namespace QiaKon.Connector.Http;

/// <summary>
/// HTTP 连接器配置选项
/// </summary>
public sealed class HttpConnectorOptions : IConnectorOptions
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public ConnectorType Type => ConnectorType.Http;

    /// <summary>
    /// 基础 URL（所有端点的公共前缀）
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 默认请求头
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; } = new();

    /// <summary>
    /// HTTP 端点配置列表
    /// </summary>
    public List<HttpEndpointConfig> Endpoints { get; } = new();

    /// <summary>
    /// 连接超时（秒）
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 最大连接数
    /// </summary>
    public int MaxConnections { get; set; } = 100;
}

