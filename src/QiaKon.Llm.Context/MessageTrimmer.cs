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

/// <summary>
/// 优先级消息裁剪器（保留高优先级消息）
/// </summary>
public sealed class PriorityMessageTrimmer : IMessageTrimmer
{
    private readonly int _systemPriority;
    private readonly int _userPriority;
    private readonly int _assistantPriority;

    public PriorityMessageTrimmer(
        int systemPriority = 100,
        int userPriority = 50,
        int assistantPriority = 30)
    {
        _systemPriority = systemPriority;
        _userPriority = userPriority;
        _assistantPriority = assistantPriority;
    }

    public void TrimToTokenLimit(List<ChatMessage> messages, int maxTokens)
    {
        if (messages.Count == 0)
            return;

        // 按优先级排序，保留高优先级消息
        var indexedMessages = messages
            .Select((m, i) => new { Message = m, Index = i, Priority = GetPriority(m) })
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Index)
            .ToList();

        var keptIndices = new HashSet<int>();
        var currentTokens = 0;

        foreach (var item in indexedMessages)
        {
            var messageTokens = EstimateMessageTokens(item.Message);
            if (currentTokens + messageTokens <= maxTokens)
            {
                keptIndices.Add(item.Index);
                currentTokens += messageTokens;
            }
        }

        // 移除不需要的消息（保持原有顺序）
        messages.Clear();
        for (int i = 0; i < indexedMessages.Count; i++)
        {
            if (keptIndices.Contains(i))
            {
                messages.Add(indexedMessages.Single(x => x.Index == i).Message);
            }
        }
    }

    private int GetPriority(ChatMessage message)
    {
        return message.Role switch
        {
            MessageRole.System => _systemPriority,
            MessageRole.User => _userPriority,
            MessageRole.Assistant => _assistantPriority,
            _ => 10
        };
    }

    private static int EstimateMessageTokens(ChatMessage message)
    {
        var textContent = string.Join(" ", message.ContentBlocks
            .OfType<TextContentBlock>()
            .Select(b => b.Text));

        var englishChars = textContent.Count(c => c <= 127);
        var chineseChars = textContent.Count(c => c > 127);
        return (englishChars / 4) + (chineseChars / 2) + 4;
    }
}

/// <summary>
/// 摘要消息裁剪器（将旧消息压缩为摘要）
/// </summary>
public sealed class SummaryMessageTrimmer : IMessageTrimmer
{
    private readonly int _summaryThreshold;
    private readonly int _keptMessagesCount;

    public SummaryMessageTrimmer(int summaryThreshold = 10, int keptMessagesCount = 5)
    {
        _summaryThreshold = summaryThreshold;
        _keptMessagesCount = keptMessagesCount;
    }

    public void TrimToTokenLimit(List<ChatMessage> messages, int maxTokens)
    {
        if (messages.Count <= _keptMessagesCount)
            return;

        var totalTokens = messages.Sum(m => EstimateMessageTokens(m));
        if (totalTokens <= maxTokens)
            return;

        // 保留最近的消息和系统消息
        var systemMessages = messages.Where(m => m.Role == MessageRole.System).ToList();
        var recentMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

        if (recentMessages.Count <= _keptMessagesCount)
            return;

        // 将旧消息压缩为摘要
        var keptRecent = recentMessages.TakeLast(_keptMessagesCount).ToList();
        var summarized = SummarizeMessages(recentMessages.Take(recentMessages.Count - _keptMessagesCount).ToList());

        messages.Clear();
        messages.AddRange(systemMessages);
        if (!string.IsNullOrEmpty(summarized))
        {
            messages.Add(ChatMessage.System($"[早期对话摘要]: {summarized}"));
        }
        messages.AddRange(keptRecent);
    }

    private static string SummarizeMessages(List<ChatMessage> messages)
    {
        if (messages.Count == 0)
            return string.Empty;

        var summaryParts = new List<string>();
        var userCount = 0;
        var assistantCount = 0;

        foreach (var msg in messages)
        {
            if (msg.Role == MessageRole.User)
                userCount++;
            else if (msg.Role == MessageRole.Assistant)
                assistantCount++;

            // 获取前100个字符作为摘要
            var text = msg.GetTextContent();
            if (text.Length > 100)
                text = text[..100] + "...";
            summaryParts.Add(text);
        }

        return $"用户问了{userCount}个问题，助手回复了{assistantCount}次。关键内容: {string.Join("; ", summaryParts.Take(3))}";
    }

    private static int EstimateMessageTokens(ChatMessage message)
    {
        var textContent = string.Join(" ", message.ContentBlocks
            .OfType<TextContentBlock>()
            .Select(b => b.Text));

        var englishChars = textContent.Count(c => c <= 127);
        var chineseChars = textContent.Count(c => c > 127);
        return (englishChars / 4) + (chineseChars / 2) + 4;
    }
}
