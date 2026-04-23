namespace QiaKon.Connector.Npgsql;

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

