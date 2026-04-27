using Microsoft.Extensions.Logging;

namespace QiaKon.Graph.Engine.Npgsql;

/// <summary>
/// PostgreSQL 图引擎配置选项
/// </summary>
public sealed class GraphEngineOptions
{
    /// <summary>
    /// 连接字符串
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// 图名称（用于多图支持）
    /// </summary>
    public string GraphName { get; set; } = "default";

    /// <summary>
    /// 节点表名前缀
    /// </summary>
    public string NodeTableName { get; set; } = "graph_nodes";

    /// <summary>
    /// 边表名前缀
    /// </summary>
    public string EdgeTableName { get; set; } = "graph_edges";

    /// <summary>
    /// 索引名称前缀
    /// </summary>
    public string IndexNamePrefix { get; set; } = "ix_graph";

    /// <summary>
    /// 命令超时时间（秒）
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 连接池最小大小
    /// </summary>
    public int MinPoolSize { get; set; } = 0;

    /// <summary>
    /// 连接池最大大小
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// 是否自动创建表结构
    /// </summary>
    public bool AutoCreateSchema { get; set; } = true;

    /// <summary>
    /// 是否自动创建索引
    /// </summary>
    public bool AutoCreateIndex { get; set; } = true;

    /// <summary>
    /// 日志记录器工厂
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }
}
