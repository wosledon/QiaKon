namespace QiaKon.Llm;

/// <summary>
/// 工具定义
/// </summary>
public sealed record ToolDefinition
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 工具描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 工具参数JSON Schema
    /// </summary>
    public string? ParametersJsonSchema { get; init; }
}

/// <summary>
/// 工具调用请求
/// </summary>
public sealed record ToolCall
{
    /// <summary>
    /// 工具调用ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 工具名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 工具参数（JSON字符串）
    /// </summary>
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// 工具调用结果
/// </summary>
public sealed record ToolCallResult
{
    /// <summary>
    /// 对应的工具调用ID
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// 工具执行结果内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 是否执行失败
    /// </summary>
    public bool IsError { get; init; }
}
