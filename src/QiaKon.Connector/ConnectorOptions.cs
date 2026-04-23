namespace QiaKon.Connector;

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

/// <summary>
/// PostgreSQL 连接器配置选项
/// </summary>
public sealed class NpgsqlConnectorOptions : IConnectorOptions
{
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public ConnectorType Type => ConnectorType.Npgsql;

    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 查询模板列表
    /// </summary>
    public List<DbQueryTemplateConfig> QueryTemplates { get; } = new();

    /// <summary>
    /// 最大连接池大小
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// 命令超时（秒）
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// 连接器配置节（用于从 appsettings.json 加载）
/// </summary>
public sealed class ConnectorConfiguration
{
    /// <summary>
    /// 连接器名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 连接器类型（Http/Npgsql）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 连接器配置（JSON 对象）
    /// </summary>
    public Dictionary<string, object> Settings { get; } = new();
}

/// <summary>
/// 连接器配置集合
/// </summary>
public sealed class ConnectorsConfiguration
{
    /// <summary>
    /// 连接器列表
    /// </summary>
    public List<ConnectorConfiguration> Connectors { get; } = new();
}

