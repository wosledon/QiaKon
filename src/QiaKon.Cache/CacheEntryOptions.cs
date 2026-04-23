namespace QiaKon.Cache;

/// <summary>
/// 缓存条目选项
/// </summary>
public sealed class CacheEntryOptions
{
    /// <summary>
    /// 绝对过期时间（从创建时开始计算）
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; set; }

    /// <summary>
    /// 滑动过期时间（最后一次访问后开始计算）
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// 缓存优先级（用于内存缓存回收策略）
    /// </summary>
    public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;

    /// <summary>
    /// 创建具有绝对过期时间的缓存条目选项
    /// </summary>
    public static CacheEntryOptions WithAbsoluteExpiration(TimeSpan expiration) =>
        new() { AbsoluteExpiration = expiration };

    /// <summary>
    /// 创建具有滑动过期时间的缓存条目选项
    /// </summary>
    public static CacheEntryOptions WithSlidingExpiration(TimeSpan expiration) =>
        new() { SlidingExpiration = expiration };

    /// <summary>
    /// 创建永不过期的缓存条目选项
    /// </summary>
    public static CacheEntryOptions NeverExpire => new();
}

/// <summary>
/// 缓存项优先级
/// </summary>
public enum CacheItemPriority
{
    Low,
    Normal,
    High,
    NeverRemove
}

/// <summary>
/// 缓存条目选项扩展方法
/// </summary>
public static class CacheEntryOptionsExtensions
{
    /// <summary>
    /// 克隆缓存条目选项
    /// </summary>
    public static CacheEntryOptions Clone(this CacheEntryOptions options)
    {
        return new CacheEntryOptions
        {
            AbsoluteExpiration = options.AbsoluteExpiration,
            SlidingExpiration = options.SlidingExpiration,
            Priority = options.Priority
        };
    }
}
