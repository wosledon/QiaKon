namespace QiaKon.Llm.Context;

/// <summary>
/// 消息裁剪器接口
/// </summary>
public interface IMessageTrimmer
{
    /// <summary>
    /// 裁剪消息列表以适应Token限制
    /// </summary>
    void TrimToTokenLimit(List<ChatMessage> messages, int maxTokens);
}

/// <summary>
/// 默认消息裁剪器（从最早的非系统消息开始移除）
/// </summary>
public sealed class DefaultMessageTrimmer : IMessageTrimmer
{
    public void TrimToTokenLimit(List<ChatMessage> messages, int maxTokens)
    {
        if (messages.Count == 0)
            return;

        // 简单策略：从最早的消息开始移除，保留系统消息
        while (EstimateTotalTokens(messages) > maxTokens && messages.Count > 1)
        {
            // 找到第一个非系统消息
            var indexToRemove = messages.FindIndex(m => m.Role != MessageRole.System);
            if (indexToRemove < 0)
                break;

            messages.RemoveAt(indexToRemove);
        }
    }

    private static int EstimateTotalTokens(List<ChatMessage> messages)
    {
        return messages.Sum(m => EstimateMessageTokens(m));
    }

    private static int EstimateMessageTokens(ChatMessage message)
    {
        var textContent = string.Join(" ", message.ContentBlocks
            .OfType<TextContentBlock>()
            .Select(b => b.Text));

        // 粗略估算
        var englishChars = textContent.Count(c => c <= 127);
        var chineseChars = textContent.Count(c => c > 127);
        return (englishChars / 4) + (chineseChars / 2) + 4;
    }
}
