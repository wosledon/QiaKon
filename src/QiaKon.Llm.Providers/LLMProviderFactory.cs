using QiaKon.Llm;

namespace QiaKon.Llm.Providers;

/// <summary>
/// LLM Provider工厂
/// </summary>
public static class LLMProviderFactory
{
    /// <summary>
    /// 根据配置创建Provider实例
    /// </summary>
    public static ILLMProvider CreateProvider(LLMProviderConfig config, HttpClient? httpClient = null)
    {
        // 验证配置
        if (!config.Validate(out var errorMessage))
        {
            throw new ArgumentException($"Provider配置无效: {errorMessage}");
        }

        // 如果没有指定ProviderType，尝试从URL自动检测
        var providerType = config.ProviderType;
        if (providerType == ProviderType.OpenAICompatible && !string.IsNullOrEmpty(config.BaseUrl))
        {
            providerType = LLMProviderConfig.DetectProviderType(config.BaseUrl);
        }

        // 根据类型创建对应的Provider
        return providerType switch
        {
            ProviderType.OpenAICompatible => new OpenAIProvider(config, httpClient),
            ProviderType.Anthropic => new AnthropicProvider(config, httpClient),
            _ => throw new NotSupportedException($"不支持的Provider类型: {providerType}")
        };
    }

    /// <summary>
    /// 快速创建OpenAI兼容的Provider（任何符合OpenAI API规范的供应商）
    /// </summary>
    public static ILLMProvider CreateOpenAICompatible(
        string apiKey,
        string? baseUrl = null,
        string? defaultModel = null,
        string? name = null,
        Dictionary<string, string>? customHeaders = null,
        int timeoutSeconds = 60)
    {
        var config = new LLMProviderConfig
        {
            Name = name,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            DefaultModel = defaultModel,
            ProviderType = ProviderType.OpenAICompatible,
            TimeoutSeconds = timeoutSeconds,
            CustomHeaders = customHeaders
        };

        return CreateProvider(config);
    }

    /// <summary>
    /// 快速创建Anthropic Provider
    /// </summary>
    public static ILLMProvider CreateAnthropic(
        string apiKey,
        string? baseUrl = null,
        string? defaultModel = null,
        string? name = null,
        Dictionary<string, string>? customHeaders = null,
        int timeoutSeconds = 60)
    {
        var config = new LLMProviderConfig
        {
            Name = name,
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? "https://api.anthropic.com",
            DefaultModel = defaultModel,
            ProviderType = ProviderType.Anthropic,
            TimeoutSeconds = timeoutSeconds,
            CustomHeaders = customHeaders
        };

        return CreateProvider(config);
    }

    /// <summary>
    /// 从环境变量创建Provider
    /// </summary>
    public static ILLMProvider CreateFromEnvironment(
        string? baseUrl = null,
        ProviderType? providerType = null)
    {
        var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "未找到API Key。请设置LLM_API_KEY、OPENAI_API_KEY或ANTHROPIC_API_KEY环境变量。");
        }

        var detectedType = providerType ?? LLMProviderConfig.DetectProviderType(baseUrl);

        var config = new LLMProviderConfig
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            ProviderType = detectedType,
            DefaultModel = Environment.GetEnvironmentVariable("LLM_DEFAULT_MODEL")
        };

        return CreateProvider(config);
    }
}
