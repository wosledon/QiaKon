namespace QiaKon.Llm;

/// <summary>
/// LLM提供者接口（向后兼容别名）
/// </summary>
public interface ILLMProvider : IAsyncDisposable, IDisposable
{
    LlmProviderType Provider { get; }
    string Model { get; }
    Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM提供者配置（向后兼容别名）
/// </summary>
public sealed record LLMProviderConfig
{
    public required LlmProviderType Provider { get; init; }
    public required string Model { get; init; }
    public required string BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public string? Organization { get; init; }
    public int MaxConcurrency { get; init; } = 5;
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;

    // 向后兼容别名
    public string DefaultModel { get => Model; init => Model = value; }
}

/// <summary>
/// LLM提供者工厂（向后兼容）
/// </summary>
public interface ILLMProviderFactory
{
    ILLMProvider CreateProvider(LLMProviderConfig config);
}
