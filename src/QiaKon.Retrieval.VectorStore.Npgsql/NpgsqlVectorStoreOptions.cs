namespace QiaKon.Retrieval.VectorStore.Npgsql;

/// <summary>
/// PostgreSQL 向量存储配置选项
/// </summary>
public sealed class NpgsqlVectorStoreOptions
{
    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 数据库 Schema（默认 "public"）
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// 最大连接池大小
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// 命令超时（秒）
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 批量插入批次大小
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// 是否自动创建 pgvector 扩展
    /// </summary>
    public bool AutoCreateExtension { get; set; } = true;

    /// <summary>
    /// 向量索引类型（默认 HNSW）
    /// </summary>
    public VectorIndexType IndexType { get; set; } = VectorIndexType.Hnsw;

    /// <summary>
    /// HNSW 索引参数 m（默认 16）
    /// </summary>
    public int HnswM { get; set; } = 16;

    /// <summary>
    /// HNSW 索引参数 ef_construction（默认 64）
    /// </summary>
    public int HnswEfConstruction { get; set; } = 64;
}

/// <summary>
/// 向量索引类型
/// </summary>
public enum VectorIndexType
{
    /// <summary>
    /// 不创建索引（小数据集适用）
    /// </summary>
    None,

    /// <summary>
    /// HNSW 近似最近邻索引
    /// </summary>
    Hnsw,

    /// <summary>
    /// IVFFlat 近似最近邻索引
    /// </summary>
    Ivfflat
}
