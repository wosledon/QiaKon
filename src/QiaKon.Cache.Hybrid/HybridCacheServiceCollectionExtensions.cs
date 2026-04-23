using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using QiaKon.Cache.Memory;
using StackExchange.Redis;

namespace QiaKon.Cache.Hybrid;

/// <summary>
/// 多级缓存 DI 注册扩展
/// </summary>
public static class HybridCacheServiceCollectionExtensions
{
    /// <summary>
    /// 注册多级缓存（混合缓存）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="redisConfiguration">Redis 连接字符串</param>
    /// <param name="configureOptions">缓存配置选项</param>
    public static IServiceCollection AddHybridCache(
        this IServiceCollection services,
        string redisConfiguration,
        Action<HybridCacheOptions>? configureOptions = null)
    {
        return AddHybridCache(services, options =>
        {
            options.Configuration = redisConfiguration;
            configureOptions?.Invoke(options.HybridCacheOptions);
        });
    }

    /// <summary>
    /// 注册多级缓存（混合缓存）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">Redis 和缓存配置</param>
    public static IServiceCollection AddHybridCache(
        this IServiceCollection services,
        Action<HybridCacheRegistrationOptions> configure)
    {
        var registrationOptions = new HybridCacheRegistrationOptions();
        configure(registrationOptions);

        // 注册 Redis 连接
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            return ConnectionMultiplexer.Connect(registrationOptions.Configuration);
        });

        // 注册 L1 内存缓存
        services.AddMemoryCache();
        services.AddSingleton<ICache, QiaKon.Cache.Memory.MemoryCache>();

        // 注册 L2 Redis 缓存
        services.AddSingleton<ICache>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            return new QiaKon.Cache.Redis.RedisCache(redis, registrationOptions.Database);
        });

        // 注册多级缓存（覆盖 ICache）
        services.AddSingleton<ICache>(sp =>
        {
            var l1Cache = sp.GetRequiredService<QiaKon.Cache.Memory.MemoryCache>();
            var l2Cache = sp.GetRequiredService<QiaKon.Cache.Redis.RedisCache>();
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();

            return new HybridCache(l1Cache, l2Cache, redis, registrationOptions.HybridCacheOptions);
        });

        return services;
    }
}

/// <summary>
/// 多级缓存注册选项
/// </summary>
public sealed class HybridCacheRegistrationOptions
{
    /// <summary>
    /// Redis 连接字符串（默认 localhost）
    /// </summary>
    public string Configuration { get; set; } = "localhost";

    /// <summary>
    /// Redis 数据库索引（默认 0）
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// 多级缓存配置选项
    /// </summary>
    public HybridCacheOptions HybridCacheOptions { get; } = HybridCacheOptions.Default;
}
