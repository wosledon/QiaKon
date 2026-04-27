using System.Runtime.CompilerServices;

namespace QiaKon.Llm.Providers;

/// <summary>
/// LLMProvider适配器：将ILlmClient适配为ILLMProvider
/// </summary>
public sealed class LlmProviderAdapter : ILLMProvider
{
    private readonly ILlmClient _client;

    public LlmProviderAdapter(ILlmClient client)
    {
        _client = client;
    }

    public LlmProviderType Provider => _client.Provider;
    public string Model => _client.Model;

    public Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        return _client.CompleteAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in _client.CompleteStreamAsync(request, cancellationToken))
        {
            yield return chunk;
        }
    }

    public void Dispose()
    {
        _client.DisposeAsync().AsTask().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}

/// <summary>
/// LLMProvider工厂适配器
/// </summary>
public sealed class LlmProviderFactoryAdapter : ILLMProviderFactory
{
    private readonly LlmClientFactory _factory;

    public LlmProviderFactoryAdapter(LlmClientFactory factory)
    {
        _factory = factory;
    }

    public ILLMProvider CreateProvider(LLMProviderConfig config)
    {
        var options = new LlmOptions
        {
            Provider = config.Provider,
            Model = config.Model,
            BaseUrl = config.BaseUrl,
            ApiKey = config.ApiKey,
            Organization = config.Organization,
            MaxConcurrency = config.MaxConcurrency,
            TimeoutSeconds = config.TimeoutSeconds,
            MaxRetries = config.MaxRetries
        };

        return new LlmProviderAdapter(_factory.CreateClient(options));
    }
}

/// <summary>
/// LLM提供者工厂实现（向后兼容）
/// </summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    public static ILLMProvider CreateProvider(LLMProviderConfig config)
    {
        var options = new LlmOptions
        {
            Provider = config.Provider,
            Model = config.Model,
            BaseUrl = config.BaseUrl,
            ApiKey = config.ApiKey,
            Organization = config.Organization,
            MaxConcurrency = config.MaxConcurrency,
            TimeoutSeconds = config.TimeoutSeconds,
            MaxRetries = config.MaxRetries
        };

        var factory = new LlmClientFactory();
        return new LlmProviderAdapter(factory.CreateClient(options));
    }

    ILLMProvider ILLMProviderFactory.CreateProvider(LLMProviderConfig config) => CreateProvider(config);
}
