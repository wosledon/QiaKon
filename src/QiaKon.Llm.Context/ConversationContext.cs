namespace QiaKon.Llm.Context;

/// <summary>
/// 对话上下文管理器
/// </summary>
public sealed class ConversationContext
{
    private readonly List<ChatMessage> _messages = new();
    private readonly int? _maxMessages;
    private readonly int? _maxTokens;
    private readonly IMessageTrimmer? _trimmer;

    public ConversationContext(
        int? maxMessages = null,
        int? maxTokens = null,
        IMessageTrimmer? trimmer = null)
    {
        _maxMessages = maxMessages;
        _maxTokens = maxTokens;
        _trimmer = trimmer ?? new DefaultMessageTrimmer();
    }

    /// <summary>
    /// 当前消息数量
    /// </summary>
    public int Count => _messages.Count;

    /// <summary>
    /// 添加消息
    /// </summary>
    public void AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        EnforceLimits();
    }

    /// <summary>
    /// 添加多条消息
    /// </summary>
    public void AddMessages(IEnumerable<ChatMessage> messages)
    {
        _messages.AddRange(messages);
        EnforceLimits();
    }

    /// <summary>
    /// 获取所有消息
    /// </summary>
    public IReadOnlyList<ChatMessage> GetMessages()
    {
        return _messages.AsReadOnly();
    }

    /// <summary>
    /// 清空对话历史
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
    }

    /// <summary>
    /// 移除最后一条消息
    /// </summary>
    public ChatMessage? RemoveLast()
    {
        if (_messages.Count == 0)
            return null;

        var last = _messages[^1];
        _messages.RemoveAt(_messages.Count - 1);
        return last;
    }

    /// <summary>
    /// 设置系统提示词（会替换或插入到第一条）
    /// </summary>
    public void SetSystemPrompt(string systemPrompt)
    {
        var existingIndex = _messages.FindIndex(m => m.Role == MessageRole.System);
        var systemMessage = ChatMessage.System(systemPrompt);

        if (existingIndex >= 0)
        {
            _messages[existingIndex] = systemMessage;
        }
        else
        {
            _messages.Insert(0, systemMessage);
        }
    }

    /// <summary>
    /// 估算当前上下文的Token数
    /// </summary>
    public int EstimateTokenCount(Func<string, int>? tokenCounter = null)
    {
        var counter = tokenCounter ?? DefaultTokenCounter;
        return _messages.Sum(m => EstimateMessageTokens(m, counter));
    }

    private void EnforceLimits()
    {
        // 基于消息数量限制
        if (_maxMessages.HasValue && _messages.Count > _maxMessages.Value)
        {
            var messagesToRemove = _messages.Count - _maxMessages.Value;
            // 保留系统消息，移除最早的用户/助手消息
            RemoveOldestMessages(messagesToRemove);
        }

        // 基于Token数限制
        if (_maxTokens.HasValue)
        {
            _trimmer?.TrimToTokenLimit(_messages, _maxTokens.Value);
        }
    }

    private void RemoveOldestMessages(int count)
    {
        var removed = 0;
        var index = 0;
        while (removed < count && index < _messages.Count)
        {
            // 不移除系统消息
            if (_messages[index].Role == MessageRole.System)
            {
                index++;
                continue;
            }

            _messages.RemoveAt(index);
            removed++;
        }
    }

    private static int EstimateMessageTokens(ChatMessage message, Func<string, int> tokenCounter)
    {
        // 简化估算：每4个字符约1个token
        var textContent = string.Join(" ", message.ContentBlocks
            .OfType<TextContentBlock>()
            .Select(b => b.Text));

        var baseTokens = tokenCounter(textContent);
        var roleTokens = 4; // 角色标记的开销
        return baseTokens + roleTokens;
    }

    private static int DefaultTokenCounter(string text)
    {
        // 粗略估算：英文每4字符1token，中文每1.5字符1token
        var englishChars = text.Count(c => c <= 127);
        var chineseChars = text.Count(c => c > 127);
        return (englishChars / 4) + (chineseChars / 2) + 1;
    }
}
