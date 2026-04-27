namespace QiaKon.Llm;

/// <summary>
/// 聊天完成响应
/// </summary>
public sealed record ChatCompletionResponse
{
    /// <summary>
    /// 响应ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 使用的模型
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// 完成原因
    /// </summary>
    public required string FinishReason { get; init; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public required ChatMessage Message { get; init; }

    /// <summary>
    /// Token使用统计
    /// </summary>
    public UsageStats? Usage { get; init; }

    /// <summary>
    /// 是否包含工具调用
    /// </summary>
    public bool HasToolCalls => Message.ContentBlocks.Any(c => c is ToolUseContentBlock);

    /// <summary>
    /// 获取工具调用列表
    /// </summary>
    public IReadOnlyList<ToolCall> GetToolCalls()
    {
        return Message.ContentBlocks
            .OfType<ToolUseContentBlock>()
            .Select(b => new ToolCall
            {
                Id = b.Id,
                Name = b.Name,
                ArgumentsJson = b.Input ?? "{}"
            })
            .ToList();
    }
}

/// <summary>
/// Token使用统计
/// </summary>
public sealed record UsageStats
{
    /// <summary>
    /// 提示Token数
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// 完成Token数
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// 总Token数
    /// </summary>
    public int TotalTokens { get; init; }
}

/// <summary>
/// 流式响应事件
/// </summary>
public sealed record StreamEvent
{
    /// <summary>
    /// 增量文本内容
    /// </summary>
    public string? DeltaText { get; init; }

    /// <summary>
    /// 是否结束
    /// </summary>
    public bool IsDone { get; init; }

    /// <summary>
    /// 完整响应（仅在结束时提供）
    /// </summary>
    public ChatCompletionResponse? FinalResponse { get; init; }
}
