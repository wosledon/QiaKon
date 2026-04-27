namespace QiaKon.Llm;

/// <summary>
/// Agent接口
/// </summary>
public interface ILlmAgent
{
    /// <summary>
    /// Agent名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行Agent
    /// </summary>
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent请求
/// </summary>
public sealed class AgentRequest
{
    /// <summary>
    /// 用户输入
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// 对话上下文消息列表
    /// </summary>
    public IReadOnlyList<ChatMessage>? Messages { get; init; }

    /// <summary>
    /// 上下文管理函数（添加消息后调用）
    /// </summary>
    public Action<ChatMessage>? OnMessageAdded { get; init; }

    /// <summary>
    /// 额外变量
    /// </summary>
    public IDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 推理参数覆盖
    /// </summary>
    public LlmInferenceOptions? InferenceOptions { get; init; }

    /// <summary>
    /// 工具列表
    /// </summary>
    public IReadOnlyList<LlmTool>? Tools { get; init; }

    /// <summary>
    /// 最大执行轮次
    /// </summary>
    public int MaxTurns { get; init; } = 10;
}

/// <summary>
/// Agent响应
/// </summary>
public sealed class AgentResponse
{
    /// <summary>
    /// 最终回复
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// 是否完成
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// 执行轮次
    /// </summary>
    public int Turns { get; init; }

    /// <summary>
    /// 工具调用结果
    /// </summary>
    public IReadOnlyList<ToolExecutionResult> ToolResults { get; init; } = [];

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// 工具定义
/// </summary>
public sealed class LlmTool
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ParametersJsonSchema { get; init; }

    /// <summary>
    /// 工具执行函数
    /// </summary>
    public required ToolExecutor Executor { get; init; }
}

/// <summary>
/// 工具执行结果
/// </summary>
public sealed class ToolExecutionResult
{
    public required string ToolName { get; init; }
    public required string ToolCallId { get; init; }
    public required string Result { get; init; }
    public bool IsError { get; init; }
}

/// <summary>
/// 工具执行器委托
/// </summary>
public delegate Task<ToolExecutionResult> ToolExecutor(
    string toolName,
    string argumentsJson,
    CancellationToken cancellationToken);

