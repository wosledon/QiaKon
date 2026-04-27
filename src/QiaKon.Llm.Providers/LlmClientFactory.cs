using System.Net.Http;

namespace QiaKon.Llm.Providers;

/// <summary>
/// LLM客户端工厂
/// </summary>
public sealed class LlmClientFactory : ILlmClientFactory
{
    private readonly HttpClient? _sharedHttpClient;

    public LlmClientFactory(HttpClient? sharedHttpClient = null)
    {
        _sharedHttpClient = sharedHttpClient;
    }

    public ILlmClient CreateClient(LlmOptions options)
    {
        var httpClient = CreateHttpClient(options);
        return CreateClientCore(httpClient, options);
    }

    public ManagedLlmClient CreateManagedClient(LlmOptions options)
    {
        var httpClient = CreateHttpClient(options);
        var client = CreateClientCore(httpClient, options);
        return new ManagedLlmClient(client, options.MaxConcurrency);
    }

    private HttpClient CreateHttpClient(LlmOptions options)
    {
        if (_sharedHttpClient != null)
        {
            return _sharedHttpClient;
        }

        var handler = new LlmRetryHandler(
            new HttpClientHandler(),
            options.MaxRetries);

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        return httpClient;
    }

    private static ILlmClient CreateClientCore(HttpClient httpClient, LlmOptions options)
    {
        return options.Provider switch
        {
            LlmProviderType.OpenAI => new OpenAiClient(httpClient, options),
            LlmProviderType.Anthropic => new AnthropicClient(httpClient, options),
            _ => throw new ArgumentException($"Unsupported provider: {options.Provider}")
        };
    }
}

/// <summary>
/// 静态工厂方法（便捷使用）
/// </summary>
public static class LlmClients
{
    private static readonly LlmClientFactory DefaultFactory = new();

    /// <summary>
    /// 创建OpenAI客户端
    /// </summary>
    public static ILlmClient OpenAI(
        string model,
        string baseUrl,
        string apiKey,
        Action<LlmOptionsBuilder>? configure = null)
    {
        var builder = new LlmOptionsBuilder(LlmProviderType.OpenAI, model, baseUrl, apiKey);
        configure?.Invoke(builder);
        return DefaultFactory.CreateClient(builder.Build());
    }

    /// <summary>
    /// 创建Anthropic客户端
    /// </summary>
    public static ILlmClient Anthropic(
        string model,
        string baseUrl,
        string apiKey,
        Action<LlmOptionsBuilder>? configure = null)
    {
        var builder = new LlmOptionsBuilder(LlmProviderType.Anthropic, model, baseUrl, apiKey);
        configure?.Invoke(builder);
        return DefaultFactory.CreateClient(builder.Build());
    }

    /// <summary>
    /// 根据配置创建客户端
    /// </summary>
    public static ILlmClient Create(LlmOptions options)
    {
        return DefaultFactory.CreateClient(options);
    }

    /// <summary>
    /// 创建带生命周期的客户端
    /// </summary>
    public static ManagedLlmClient CreateManaged(LlmOptions options)
    {
        return DefaultFactory.CreateManagedClient(options);
    }
}

/// <summary>
/// LLM选项构建器
/// </summary>
public sealed class LlmOptionsBuilder
{
    private readonly LlmOptions _options;

    public LlmOptionsBuilder(LlmProviderType provider, string model, string baseUrl, string apiKey)
    {
        _options = new LlmOptions
        {
            Provider = provider,
            Model = model,
            BaseUrl = baseUrl,
            ApiKey = apiKey
        };
    }

    private LlmOptionsBuilder(LlmOptions options)
    {
        _options = options;
    }

    public LlmOptionsBuilder WithMaxConcurrency(int maxConcurrency)
    {
        return new LlmOptionsBuilder(_options with { MaxConcurrency = maxConcurrency });
    }

    public LlmOptionsBuilder WithTimeout(int seconds)
    {
        return new LlmOptionsBuilder(_options with { TimeoutSeconds = seconds });
    }

    public LlmOptionsBuilder WithMaxRetries(int retries)
    {
        return new LlmOptionsBuilder(_options with { MaxRetries = retries });
    }

    public LlmOptionsBuilder WithTemperature(double temperature)
    {
        return new LlmOptionsBuilder(_options with { InferenceOptions = _options.InferenceOptions with { Temperature = temperature } });
    }

    public LlmOptionsBuilder WithMaxTokens(int maxTokens)
    {
        return new LlmOptionsBuilder(_options with { InferenceOptions = _options.InferenceOptions with { MaxTokens = maxTokens } });
    }

    public LlmOptionsBuilder WithTopP(double topP)
    {
        return new LlmOptionsBuilder(_options with { InferenceOptions = _options.InferenceOptions with { TopP = topP } });
    }

    public LlmOptionsBuilder WithStopSequences(params string[] stopSequences)
    {
        return new LlmOptionsBuilder(_options with { InferenceOptions = _options.InferenceOptions with { StopSequences = stopSequences.ToList() } });
    }

    public LlmOptionsBuilder EnableDetailedLogging()
    {
        return new LlmOptionsBuilder(_options with { EnableDetailedLogging = true });
    }

    public LlmOptionsBuilder WithOrganization(string org)
    {
        return new LlmOptionsBuilder(_options with { Organization = org });
    }

    public LlmOptions Build() => _options;
}
