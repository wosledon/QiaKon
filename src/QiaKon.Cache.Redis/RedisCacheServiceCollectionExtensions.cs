using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace QiaKon.Cache.Redis;

/// <summary>
/// Redis 缓存服务注册扩展
/// </summary>
public static class RedisCacheServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Redis 缓存服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">Redis 连接字符串</param>
    /// <param name="database">Redis 数据库索引</param>
    /// <param name="keyPrefix">缓存键前缀（默认 qiakon:）</param>
    public static IServiceCollection AddQiaKonRedisCache(
        this IServiceCollection services,
        string configuration,
        int database = 0,
        string keyPrefix = "qiakon:")
    {
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(configuration));
        services.AddSingleton<ICache>(sp =>
        {
            var connection = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisCache(connection, database, keyPrefix);
        });
        return services;
    }

    /// <summary>
    /// 添加 Redis 缓存服务（使用配置委托）
    /// </summary>
    public static IServiceCollection AddQiaKonRedisCache(
        this IServiceCollection services,
        Action<ConfigurationOptions> configure,
        string keyPrefix = "qiakon:")
    {
        var options = new ConfigurationOptions();
        configure(options);

        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton<ICache>(sp =>
        {
            var connection = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisCache(connection, options.DefaultDatabase ?? 0, keyPrefix);
        });
        return services;
    }
}
