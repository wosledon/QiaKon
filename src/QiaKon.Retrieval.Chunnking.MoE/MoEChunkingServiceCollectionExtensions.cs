using Microsoft.Extensions.DependencyInjection;
using QiaKon.Retrieval.Chunnking;

namespace QiaKon.Retrieval.Chunnking.MoE;

/// <summary>
/// MoE 智能分块服务注册扩展
/// </summary>
public static class MoEChunkingServiceCollectionExtensions
{
    /// <summary>
    /// 注册 MoE 智能分块策略工厂（单例）
    /// </summary>
    public static IServiceCollection AddMoEChunking(this IServiceCollection services)
    {
        services.AddSingleton<IMoEChunkingStrategyFactory, MoEChunkingStrategyFactory>();
        return services;
    }

    /// <summary>
    /// 注册 MoE 智能分块选项
    /// </summary>
    public static IServiceCollection AddMoEChunkingOptions(
        this IServiceCollection services,
        Action<MoEChunkingOptions> configureOptions)
    {
        var options = new MoEChunkingOptions();
        configureOptions(options);

        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// 注册 MoE 智能分块选项
    /// </summary>
    public static IServiceCollection AddMoEChunkingOptions(
        this IServiceCollection services,
        MoEChunkingOptions options)
    {
        services.AddSingleton(options);
        return services;
    }
}
