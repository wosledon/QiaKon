namespace QiaKon.Llm;

/// <summary>
/// Provider类型枚举
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// OpenAI兼容API（包括OpenAI、Azure OpenAI、LocalAI、Ollama等）
    /// </summary>
    OpenAICompatible,

    /// <summary>
    /// Anthropic Claude API
    /// </summary>
    Anthropic
}

/// <summary>
/// LLM Provider接口
/// </summary>
public interface ILLMProvider : IDisposable
{
    /// <summary>
    /// Provider名称
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Provider类型
    /// </summary>
    ProviderType ProviderType { get; }

    /// <summary>
    /// 执行聊天完成请求
    /// </summary>
    Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行流式聊天完成请求
    /// </summary>
    IAsyncEnumerable<StreamEvent> CompleteStreamingAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取支持的模型列表
    /// </summary>
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM Provider配置
/// </summary>
public record LLMProviderConfig
{
    /// <summary>
    /// 配置名称（用于标识此配置）
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Provider类型
    /// </summary>
    public ProviderType ProviderType { get; init; } = ProviderType.OpenAICompatible;

    /// <summary>
    /// API Key
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Base URL（可选，用于自定义端点）
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// 默认模型
    /// </summary>
    public string? DefaultModel { get; init; }

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 自定义HTTP Headers（某些供应商需要额外的headers）
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; init; }

    /// <summary>
    /// API版本（某些供应商需要，如Azure需要api-version）
    /// </summary>
    public string? ApiVersion { get; init; }

    /// <summary>
    /// 从URL自动检测Provider类型
    /// </summary>
    public static ProviderType DetectProviderType(string? baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl))
            return ProviderType.OpenAICompatible;

        var url = baseUrl.ToLowerInvariant();

        // Anthropic
        if (url.Contains("anthropic.com") || url.Contains("api.anthropic"))
            return ProviderType.Anthropic;

        // Azure OpenAI
        if (url.Contains("openai.azure.com"))
            return ProviderType.OpenAICompatible;

        // Ollama
        if (url.Contains("ollama") || url.Contains(":11434"))
            return ProviderType.OpenAICompatible;

        // LocalAI
        if (url.Contains("localai"))
            return ProviderType.OpenAICompatible;

        // 默认OpenAI兼容
        return ProviderType.OpenAICompatible;
    }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public bool Validate(out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            errorMessage = "ApiKey不能为空";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
