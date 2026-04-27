namespace QiaKon.Llm;

/// <summary>
/// 消息角色
/// </summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool,
}

/// <summary>
/// 内容块基类
/// </summary>
public abstract class ContentBlock;

/// <summary>
/// 文本内容块
/// </summary>
public sealed class TextContentBlock : ContentBlock
{
    public required string Text { get; init; }
}

/// <summary>
/// 图片内容块
/// </summary>
public sealed class ImageContentBlock : ContentBlock
{
    public required string Url { get; init; }
    public string? MediaType { get; init; }
}

/// <summary>
/// ToolCall内容块
/// </summary>
public sealed class ToolCallContentBlock : ContentBlock
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ArgumentsJson { get; init; }
}

/// <summary>
/// ToolResult内容块
/// </summary>
public sealed class ToolResultContentBlock : ContentBlock
{
    public required string ToolCallId { get; init; }
    public string? Content { get; init; }
    public bool IsError { get; init; }
}

/// <summary>
/// 内容块工厂
/// </summary>
public static class ContentBlocks
{
    public static TextContentBlock Text(string text) => new() { Text = text };
    public static ImageContentBlock Image(string url, string? mediaType = null) => new() { Url = url, MediaType = mediaType };
    public static ToolCallContentBlock ToolCall(string id, string name, string? argumentsJson = null) => new() { Id = id, Name = name, ArgumentsJson = argumentsJson };
    public static ToolResultContentBlock ToolResult(string toolCallId, string? content = null, bool isError = false) => new() { ToolCallId = toolCallId, Content = content, IsError = isError };
}

/// <summary>
/// 聊天消息
/// </summary>
public sealed class ChatMessage
{
    public MessageRole Role { get; init; }
    public IReadOnlyList<ContentBlock> ContentBlocks { get; init; } = [];
    public string? Name { get; init; }
    public string? ToolCallId { get; init; }

    public static ChatMessage System(string content) => new() { Role = MessageRole.System, ContentBlocks = [new TextContentBlock { Text = content }] };
    public static ChatMessage User(string content) => new() { Role = MessageRole.User, ContentBlocks = [new TextContentBlock { Text = content }] };
    public static ChatMessage Assistant(string content, string? toolCallId = null) => new() { Role = MessageRole.Assistant, ContentBlocks = [new TextContentBlock { Text = content }], ToolCallId = toolCallId };
    public static ChatMessage Tool(string content, string toolCallId) => new() { Role = MessageRole.Tool, ContentBlocks = [new TextContentBlock { Text = content }], ToolCallId = toolCallId };

    public static ChatMessage User(IEnumerable<ContentBlock> blocks) => new() { Role = MessageRole.User, ContentBlocks = blocks.ToList() };
    public static ChatMessage Assistant(IEnumerable<ContentBlock> blocks) => new() { Role = MessageRole.Assistant, ContentBlocks = blocks.ToList() };

    public string GetTextContent()
    {
        return string.Join("", ContentBlocks.OfType<TextContentBlock>().Select(b => b.Text));
    }
}
