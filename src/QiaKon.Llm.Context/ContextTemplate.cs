namespace QiaKon.Llm.Context;

/// <summary>
/// 上下文模板（可复用的上下文模式）
/// </summary>
public sealed record ContextTemplate
{
    /// <summary>
    /// 模板名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 系统提示词模板
    /// </summary>
    public string? SystemPromptTemplate { get; init; }

    /// <summary>
    /// 初始消息模板
    /// </summary>
    public IReadOnlyList<ChatMessage>? InitialMessages { get; init; }

    /// <summary>
    /// 最大消息数
    /// </summary>
    public int? MaxMessages { get; init; }

    /// <summary>
    /// 最大Token数
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 创建新的上下文实例
    /// </summary>
    public ConversationContext CreateContext(
        IDictionary<string, string>? variables = null,
        IMessageTrimmer? trimmer = null)
    {
        var context = new ConversationContext(
            maxMessages: MaxMessages,
            maxTokens: MaxTokens,
            trimmer: trimmer);

        // 应用系统提示词
        if (!string.IsNullOrEmpty(SystemPromptTemplate))
        {
            var systemPrompt = variables != null
                ? ApplyVariables(SystemPromptTemplate, variables)
                : SystemPromptTemplate;

            context.SetSystemPrompt(systemPrompt);
        }

        // 添加初始消息
        if (InitialMessages != null)
        {
            foreach (var message in InitialMessages)
            {
                context.AddMessage(message);
            }
        }

        return context;
    }

    private static string ApplyVariables(string template, IDictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{key}}}", value);
        }
        return result;
    }
}

/// <summary>
/// 上下文模板注册表
/// </summary>
public sealed class ContextTemplateRegistry
{
    private readonly Dictionary<string, ContextTemplate> _templates = new();

    public void Register(ContextTemplate template)
    {
        _templates[template.Name] = template;
    }

    public ContextTemplate? Get(string name)
    {
        return _templates.GetValueOrDefault(name);
    }

    public bool TryGet(string name, out ContextTemplate? template)
    {
        return _templates.TryGetValue(name, out template);
    }

    public IReadOnlyList<string> ListTemplateNames()
    {
        return _templates.Keys.ToList();
    }
}
