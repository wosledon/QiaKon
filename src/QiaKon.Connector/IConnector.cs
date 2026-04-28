namespace QiaKon.Connector;

/// <summary>
/// 连接器基础接口（配置驱动）
/// </summary>
public interface IConnector : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 连接器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 连接器类型
    /// </summary>
    ConnectorType Type { get; }

    /// <summary>
    /// 连接状态
    /// </summary>
    ConnectorState State { get; }

    /// <summary>
    /// 初始化连接器
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭连接器
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// HTTP 连接器接口
/// </summary>
public interface IHttpConnector : IConnector
{
    /// <summary>
    /// 执行 HTTP 端点调用
    /// </summary>
    /// <param name="endpointName">端点名称（配置中定义）</param>
    /// <param name="parameters">请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应结果</returns>
    Task<ConnectorResponse> ExecuteAsync(
        string endpointName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行 HTTP 端点调用（泛型返回）
    /// </summary>
    Task<T?> ExecuteAsync<T>(
        string endpointName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// 数据库连接器接口
/// </summary>
public interface IDbConnector : IConnector
{
    /// <summary>
    /// 执行查询模板
    /// </summary>
    /// <param name="templateName">模板名称（配置中定义）</param>
    /// <param name="parameters">查询参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果</returns>
    Task<ConnectorResponse> QueryAsync(
        string templateName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行查询模板（泛型返回）
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string templateName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// 执行命令模板（INSERT/UPDATE/DELETE）
    /// </summary>
    Task<int> ExecuteCommandAsync(
        string templateName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 连接器配置接口
/// </summary>
public interface IConnectorOptions
{
    /// <summary>
    /// 连接器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 连接器类型
    /// </summary>
    ConnectorType Type { get; }
}

/// <summary>
/// 连接器工厂接口
/// </summary>
public interface IConnectorFactory
{
    IConnector Create(IConnectorOptions options);
}

/// <summary>
/// 连接器注册表
/// </summary>
public interface IConnectorRegistry
{
    IConnector Get(string name);
    bool TryGet(string name, out IConnector? connector);
    IEnumerable<string> GetAllNames();
}

/// <summary>
/// 连接器管理器接口
/// </summary>
public interface IConnectorManager
{
    void Register(IConnector connector);
    IConnector? Get(string name);
    Task InitializeAllAsync(CancellationToken cancellationToken = default);
    Task CloseAllAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, HealthCheckResult>> HealthCheckAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 连接器类型
/// </summary>
public enum ConnectorType
{
    Http,
    Npgsql,
    Redis,
    MessageQueue,
    Custom
}

/// <summary>
/// 连接器状态
/// </summary>
public enum ConnectorState
{
    Disconnected,
    Connecting,
    Connected,
    Healthy,
    Unhealthy,
    Closed
}

/// <summary>
/// 健康检查结果
/// </summary>
public sealed record HealthCheckResult(
    bool IsHealthy,
    string? Message = null,
    TimeSpan? Latency = null);

/// <summary>
/// 连接器响应结果
/// </summary>
public sealed record ConnectorResponse(
    bool IsSuccess,
    object? Data = null,
    string? ErrorMessage = null,
    int StatusCode = 200,
    IReadOnlyDictionary<string, string>? Headers = null);

/// <summary>
/// HTTP 端点配置
/// </summary>
public sealed class HttpEndpointConfig
{
    /// <summary>
    /// 端点名称（唯一标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法（GET/POST/PUT/DELETE/PATCH）
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// 请求 URL（支持模板变量 {param}）
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 请求头（键值对）
    /// </summary>
    public Dictionary<string, string> Headers { get; } = new();

    /// <summary>
    /// 请求体模板（JSON 格式，支持模板变量）
    /// </summary>
    public string? BodyTemplate { get; set; }

    /// <summary>
    /// 查询参数模板
    /// </summary>
    public Dictionary<string, string> QueryParameters { get; } = new();

    /// <summary>
    /// 响应数据路径（JSONPath，用于提取嵌套数据）
    /// </summary>
    public string? ResponseDataPath { get; set; }

    /// <summary>
    /// 成功状态码（默认 200-299）
    /// </summary>
    public string? SuccessStatusCodes { get; set; }

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 0;
}

/// <summary>
/// 数据库查询模板配置
/// </summary>
public sealed class DbQueryTemplateConfig
{
    /// <summary>
    /// 模板名称（唯一标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SQL 模板（支持参数 @param）
    /// </summary>
    public string SqlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 查询类型（Query/Command/StoredProcedure）
    /// </summary>
    public string CommandType { get; set; } = "Query";

    /// <summary>
    /// 参数定义
    /// </summary>
    public List<DbParameterConfig> Parameters { get; } = new();

    /// <summary>
    /// 结果列映射
    /// </summary>
    public Dictionary<string, string> ResultMapping { get; } = new();

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCache { get; set; } = false;

    /// <summary>
    /// 缓存 TTL（秒）
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 60;
}

/// <summary>
/// 数据库参数配置
/// </summary>
public sealed class DbParameterConfig
{
    /// <summary>
    /// 参数名称（对应 SQL 中的 @param）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数类型（NpgsqlDbType 字符串）
    /// </summary>
    public string DbType { get; set; } = "Text";

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; set; }
}

