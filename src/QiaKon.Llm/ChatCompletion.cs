namespace QiaKon.Llm;

/// <summary>
/// 模型供应商类型
/// </summary>
public enum LlmProviderType
{
    OpenAI,
    Anthropic,
}

/// <summary>
/// LLM配置选项
/// </summary>
public sealed record LlmOptions
{
    public required LlmProviderType Provider { get; init; }
    public required string Model { get; init; }
    public required string BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public string? Organization { get; init; }

    /// <summary>
    /// 最大并发数（默认5）
    /// </summary>
    public int MaxConcurrency { get; init; } = 5;

    /// <summary>
    /// 请求超时（秒，默认30）
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// 最大重试次数（默认3）
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 是否启用详细日志
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = false;

    /// <summary>
    /// 默认推理参数
    /// </summary>
    public LlmInferenceOptions InferenceOptions { get; init; } = new();
}

/// <summary>
/// 推理参数
/// </summary>
public sealed record LlmInferenceOptions
{
    /// <summary>
    /// 生成的最大Token数
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 温度参数（0-2）
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Top-P采样
    /// </summary>
    public double TopP { get; init; } = 1.0;

    /// <summary>
    /// 停止序列
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// 是否流式输出
    /// </summary>
    public bool Stream { get; init; } = false;
}

/// <summary>
/// Chat补全请求
/// </summary>
public sealed class ChatCompletionRequest
{
    public required string Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public LlmInferenceOptions? InferenceOptions { get; init; }
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }
    public string? ToolChoice { get; init; }

    // 向后兼容属性
    public double Temperature
    {
        get => InferenceOptions?.Temperature ?? 0.7;
        init
        {
            InferenceOptions = InferenceOptions ?? new LlmInferenceOptions();
            InferenceOptions = InferenceOptions with { Temperature = value };
        }
    }

    public int MaxTokens
    {
        get => InferenceOptions?.MaxTokens ?? 1024;
        init
        {
            InferenceOptions = InferenceOptions ?? new LlmInferenceOptions();
            InferenceOptions = InferenceOptions with { MaxTokens = value };
        }
    }
}

/// <summary>
/// Tool定义
/// </summary>
public sealed class ToolDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ParametersJsonSchema { get; init; }
}

/// <summary>
/// Chat补全响应
/// </summary>
public sealed class ChatCompletionResponse
{
    public required string Id { get; init; }
    public required string Model { get; init; }
    public required ChatMessage Message { get; init; }
    public int UsagePromptTokens { get; init; }
    public int UsageCompletionTokens { get; init; }
    public int UsageTotalTokens { get; init; }
    public string? FinishReason { get; init; }
}

/// <summary>
/// 流式Chunk
/// </summary>
public sealed class ChatCompletionChunk
{
    public string? Id { get; init; }
    public string? Model { get; init; }
    public string? Content { get; init; }
    public MessageRole? Role { get; init; }
    public bool IsComplete { get; init; }
    public string? FinishReason { get; init; }
}
