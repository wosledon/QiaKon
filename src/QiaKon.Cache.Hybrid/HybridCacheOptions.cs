namespace QiaKon.Cache.Hybrid;

/// <summary>
/// 多级缓存配置选项
/// </summary>
public sealed class HybridCacheOptions
{
    /// <summary>
    /// 默认配置
    /// </summary>
    public static HybridCacheOptions Default => new()
    {
        L1Ttl = TimeSpan.FromMinutes(3),
        L2Ttl = TimeSpan.FromMinutes(30),
        L1Options = null,
        L2Options = null,
        EnableVersionCheck = true,
        EnableL1Backfill = true,
        InvalidationChannel = "qiaKon:cache:invalidation",
        VersionPrefix = "qiaKon:cache:version:"
    };

    /// <summary>
    /// L1 缓存 TTL（默认 3 分钟）
    /// </summary>
    public TimeSpan L1Ttl { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// L2 缓存 TTL（默认 30 分钟）
    /// </summary>
    public TimeSpan L2Ttl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// L1 缓存条目选项（如果设置，将覆盖 L1Ttl）
    /// </summary>
    public CacheEntryOptions? L1Options { get; set; }

    /// <summary>
    /// L2 缓存条目选项（如果设置，将覆盖 L2Ttl）
    /// </summary>
    public CacheEntryOptions? L2Options { get; set; }

    /// <summary>
    /// 是否在读取时校验 L2 版本（默认启用）
    /// </summary>
    public bool EnableVersionCheck { get; set; } = true;

    /// <summary>
    /// 是否启用 L1 回填（从 L2 命中时回填到 L1）
    /// </summary>
    public bool EnableL1Backfill { get; set; } = true;

    /// <summary>
    /// Redis Pub/Sub 失效通知通道名称
    /// </summary>
    public string InvalidationChannel { get; set; } = "qiaKon:cache:invalidation";

    /// <summary>
    /// Redis 版本号存储前缀
    /// </summary>
    public string VersionPrefix { get; set; } = "qiaKon:cache:version:";
}
