using Microsoft.Extensions.DependencyInjection;
using QiaKon.Llm;
using QiaKon.Retrieval.Chunnking;

namespace QiaKon.Retrieval.Chunnking.MoE;

/// <summary>
/// MoE 智能分块服务注册扩展
/// </summary>
public static class MoEChunkingServiceCollectionExtensions
{
    /// <summary>
    /// 注册 MoE 智能分块策略
    ///
    /// 配置驱动说明：
    /// - 如果在 <paramref name="configureOptions"/> 中设置了 ProviderConfig，
    ///   MoE 会根据该配置独立创建 LLM Provider 实例（无需预先注册 ILLMProvider）
    /// - 如果未设置 ProviderConfig，则必须确保 DI 容器中已注册了默认 ILLMProvider
    /// </summary>
    public static IServiceCollection AddMoEChunking(
        this IServiceCollection services,
        Action<MoEChunkingOptions>? configureOptions = null)
    {
        var options = new MoEChunkingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<IChunkingStrategy, MoEChunkingStrategy>();
        return services;
    }

    /// <summary>
    /// 注册 MoE 智能分块策略（使用显式配置）
    ///
    /// 配置驱动说明：
    /// - 如果 <paramref name="options"/> 中设置了 ProviderConfig，
    ///   MoE 会根据该配置独立创建 LLM Provider 实例
    /// - 如果未设置 ProviderConfig，则必须确保 DI 容器中已注册了默认 ILLMProvider
    /// </summary>
    public static IServiceCollection AddMoEChunking(
        this IServiceCollection services,
        MoEChunkingOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IChunkingStrategy, MoEChunkingStrategy>();
        return services;
    }
}
