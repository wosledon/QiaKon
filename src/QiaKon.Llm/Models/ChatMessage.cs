namespace QiaKon.Llm;

/// <summary>
/// 消息角色枚举
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// 系统消息
    /// </summary>
    System,

    /// <summary>
    /// 用户消息
    /// </summary>
    User,

    /// <summary>
    /// 助手消息
    /// </summary>
    Assistant,

    /// <summary>
    /// 工具消息
    /// </summary>
    Tool
}

/// <summary>
/// 消息内容块类型
/// </summary>
public enum ContentBlockType
{
    /// <summary>
    /// 纯文本
    /// </summary>
    Text,

    /// <summary>
    /// 图片
    /// </summary>
    Image,

    /// <summary>
    /// 工具调用
    /// </summary>
    ToolUse,

    /// <summary>
    /// 工具调用结果
    /// </summary>
    ToolResult
}

/// <summary>
/// 消息内容块
/// </summary>
public abstract record ContentBlock(ContentBlockType Type);

/// <summary>
/// 文本内容块
/// </summary>
public sealed record TextContentBlock(string Text) : ContentBlock(ContentBlockType.Text);

/// <summary>
/// 图片内容块
/// </summary>
public sealed record ImageContentBlock(string Url, string? MediaType = null) : ContentBlock(ContentBlockType.Image);

/// <summary>
/// 工具调用内容块
/// </summary>
public sealed record ToolUseContentBlock(string Id, string Name, string? Input = null) : ContentBlock(ContentBlockType.ToolUse);

/// <summary>
/// 工具结果内容块
/// </summary>
public sealed record ToolResultContentBlock(string ToolUseId, string? Content = null, bool IsError = false) : ContentBlock(ContentBlockType.ToolResult);

/// <summary>
/// 聊天消息
/// </summary>
public sealed record ChatMessage
{
    public MessageRole Role { get; init; }
    public IReadOnlyList<ContentBlock> ContentBlocks { get; init; } = Array.Empty<ContentBlock>();

    /// <summary>
    /// 快捷构造函数 - 纯文本消息
    /// </summary>
    public static ChatMessage CreateText(MessageRole role, string text)
    {
        return new ChatMessage
        {
            Role = role,
            ContentBlocks = new[] { new TextContentBlock(text) }
        };
    }

    /// <summary>
    /// 快捷构造函数 - 系统消息
    /// </summary>
    public static ChatMessage System(string text) => CreateText(MessageRole.System, text);

    /// <summary>
    /// 快捷构造函数 - 用户消息
    /// </summary>
    public static ChatMessage User(string text) => CreateText(MessageRole.User, text);

    /// <summary>
    /// 快捷构造函数 - 助手消息
    /// </summary>
    public static ChatMessage Assistant(string text) => CreateText(MessageRole.Assistant, text);

    /// <summary>
    /// 获取纯文本内容（如果只有一个文本块）
    /// </summary>
    public string? GetTextContent()
    {
        if (ContentBlocks.Count == 1 && ContentBlocks[0] is TextContentBlock textBlock)
        {
            return textBlock.Text;
        }
        return null;
    }
}
