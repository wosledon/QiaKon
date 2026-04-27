using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Llm;

/// <summary>
/// LLM服务注册扩展
/// </summary>
public static class LLMServiceCollectionExtensions
{
    /// <summary>
    /// 注册LLM Provider工厂
    /// </summary>
    public static IServiceCollection AddLLMProvider<TProvider>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, LLMProviderConfig, TProvider> factory,
        LLMProviderConfig config,
        bool isDefault = false)
        where TProvider : class, ILLMProvider
    {
        services.AddSingleton<ILLMProvider>(sp => factory(sp, config));

        if (isDefault)
        {
            services.AddSingleton(sp => sp.GetRequiredService<ILLMProvider>());
        }

        return services;
    }

    /// <summary>
    /// 注册命名LLM Provider
    /// </summary>
    public static IServiceCollection AddNamedLLMProvider<TProvider>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, LLMProviderConfig, TProvider> factory,
        LLMProviderConfig config)
        where TProvider : class, ILLMProvider
    {
        services.AddKeyedSingleton<ILLMProvider>(name, (sp, key) => factory(sp, config));
        return services;
    }
}
