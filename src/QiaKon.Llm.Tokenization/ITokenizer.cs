namespace QiaKon.Llm.Tokenization;

/// <summary>
/// Token 计数结果
/// </summary>
public sealed class TokenCount
{
    /// <summary>
    /// 输入 tokens 数量
    /// </summary>
    public required int InputTokens { get; init; }

    /// <summary>
    /// 输出 tokens 数量（可选）
    /// </summary>
    public int? OutputTokens { get; init; }

    /// <summary>
    /// 总 tokens 数量
    /// </summary>
    public int Total => InputTokens + (OutputTokens ?? 0);

    /// <summary>
    /// 创建只包含输入的结果
    /// </summary>
    public static TokenCount ForInput(int inputTokens) => new() { InputTokens = inputTokens };

    /// <summary>
    /// 创建包含输入和输出的结果
    /// </summary>
    public static TokenCount ForInputOutput(int inputTokens, int outputTokens)
        => new() { InputTokens = inputTokens, OutputTokens = outputTokens };
}

/// <summary>
/// LLM Token 计数器接口
/// </summary>
public interface ITokenizer
{
    /// <summary>
    /// 计算文本的 token 数量
    /// </summary>
    int CountTokens(string text);

    /// <summary>
    /// 计算消息列表的 token 数量
    /// </summary>
    int CountMessages(IEnumerable<ChatMessage> messages);

    /// <summary>
    /// 获取 tokenizer 名称
    /// </summary>
    string Name { get; }
}
