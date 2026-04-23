using Microsoft.Extensions.DependencyInjection;
using McOptions = Microsoft.Extensions.Caching.Memory.MemoryCacheOptions;

namespace QiaKon.Cache.Memory;

/// <summary>
/// 内存缓存服务注册扩展
/// </summary>
public static class MemoryCacheServiceCollectionExtensions
{
    /// <summary>
    /// 添加内存缓存服务
    /// </summary>
    public static IServiceCollection AddQiaKonMemoryCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICache, MemoryCache>();
        return services;
    }

    /// <summary>
    /// 添加内存缓存服务（带配置）
    /// </summary>
    public static IServiceCollection AddQiaKonMemoryCache(
        this IServiceCollection services,
        Action<McOptions> configure)
    {
        services.AddMemoryCache(configure);
        services.AddSingleton<ICache, MemoryCache>();
        return services;
    }
}
