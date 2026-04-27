using Microsoft.Extensions.DependencyInjection;
using QiaKon.Llm;

namespace QiaKon.Llm.Providers;

/// <summary>
/// Provider服务注册扩展
/// </summary>
public static class ProviderServiceCollectionExtensions
{
    /// <summary>
    /// 注册OpenAI Provider（或任何OpenAI兼容的API）
    /// </summary>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        LLMProviderConfig config,
        bool isDefault = false)
    {
        services.AddHttpClient<OpenAIProvider>();
        services.AddSingleton<ILLMProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            return new OpenAIProvider(config, httpClient);
        });

        if (isDefault)
        {
            services.AddSingleton(sp => sp.GetRequiredService<ILLMProvider>());
        }

        return services;
    }

    /// <summary>
    /// 快速注册OpenAI兼容的Provider（支持任意符合OpenAI规范的供应商）
    /// </summary>
    public static IServiceCollection AddOpenAICompatibleProvider(
        this IServiceCollection services,
        string apiKey,
        string? baseUrl = null,
        string? defaultModel = null,
        string? name = null,
        Dictionary<string, string>? customHeaders = null,
        bool isDefault = false)
    {
        var config = new LLMProviderConfig
        {
            Name = name,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            DefaultModel = defaultModel,
            ProviderType = ProviderType.OpenAICompatible,
            CustomHeaders = customHeaders
        };

        return services.AddOpenAIProvider(config, isDefault);
    }

    /// <summary>
    /// 注册Anthropic Provider
    /// </summary>
    public static IServiceCollection AddAnthropicProvider(
        this IServiceCollection services,
        LLMProviderConfig config,
        bool isDefault = false)
    {
        services.AddHttpClient<AnthropicProvider>();
        services.AddSingleton<ILLMProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            return new AnthropicProvider(config, httpClient);
        });

        if (isDefault)
        {
            services.AddSingleton(sp => sp.GetRequiredService<ILLMProvider>());
        }

        return services;
    }

    /// <summary>
    /// 根据配置自动注册Provider（根据ProviderType创建对应的实现）
    /// </summary>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        LLMProviderConfig config,
        bool isDefault = false)
    {
        services.AddSingleton<ILLMProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            return LLMProviderFactory.CreateProvider(config, httpClient);
        });

        if (isDefault)
        {
            services.AddSingleton(sp => sp.GetRequiredService<ILLMProvider>());
        }

        return services;
    }

    /// <summary>
    /// 注册多个Provider（通过配置列表）
    /// </summary>
    public static IServiceCollection AddLLMProviders(
        this IServiceCollection services,
        IEnumerable<LLMProviderConfig> configs,
        string? defaultProviderName = null)
    {
        foreach (var config in configs)
        {
            var isDefault = config.Name == defaultProviderName;

            // 使用配置名称作为Key
            services.AddKeyedSingleton<ILLMProvider>(config.Name, (sp, key) =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient();
                return LLMProviderFactory.CreateProvider(config, httpClient);
            });

            // 如果是默认，也注册为无Key的服务
            if (isDefault)
            {
                services.AddSingleton<ILLMProvider>(sp =>
                {
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    var httpClient = httpClientFactory.CreateClient();
                    return LLMProviderFactory.CreateProvider(config, httpClient);
                });
            }
        }

        return services;
    }

    /// <summary>
    /// 注册命名Provider
    /// </summary>
    public static IServiceCollection AddNamedProvider<TProvider>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, HttpClient, TProvider> factory,
        LLMProviderConfig config)
        where TProvider : class, ILLMProvider
    {
        services.AddKeyedSingleton<ILLMProvider>(name, (sp, key) =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            return factory(sp, httpClient);
        });

        return services;
    }
}
