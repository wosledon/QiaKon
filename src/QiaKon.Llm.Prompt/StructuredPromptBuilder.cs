namespace QiaKon.Llm.Prompt;

/// <summary>
/// 结构化Prompt构建器
/// </summary>
public sealed class StructuredPromptBuilder
{
    private string? _systemPrompt;
    private readonly List<Section> _sections = new();
    private readonly Dictionary<string, string> _variables = new();

    /// <summary>
    /// 设置系统提示词
    /// </summary>
    public StructuredPromptBuilder WithSystemPrompt(string prompt)
    {
        _systemPrompt = prompt;
        return this;
    }

    /// <summary>
    /// 添加章节
    /// </summary>
    public StructuredPromptBuilder AddSection(
        string title,
        string content,
        int importance = 0)
    {
        _sections.Add(new Section(title, content, importance));
        return this;
    }

    /// <summary>
    /// 添加变量
    /// </summary>
    public StructuredPromptBuilder WithVariable(string name, string value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// 添加示例
    /// </summary>
    public StructuredPromptBuilder AddExample(string input, string output)
    {
        var exampleContent = $"""
            输入: {input}
            输出: {output}
            """;
        return AddSection("示例", exampleContent);
    }

    /// <summary>
    /// 添加约束
    /// </summary>
    public StructuredPromptBuilder AddConstraint(string constraint)
    {
        var existingSection = _sections.FirstOrDefault(s => s.Title == "约束");
        if (existingSection != null)
        {
            _sections.Remove(existingSection);
            _sections.Add(existingSection with { Content = existingSection.Content + "\n- " + constraint });
        }
        else
        {
            AddSection("约束", "- " + constraint);
        }
        return this;
    }

    /// <summary>
    /// 构建最终Prompt
    /// </summary>
    public string Build()
    {
        var sb = new System.Text.StringBuilder();

        // 按重要性排序
        var sortedSections = _sections.OrderByDescending(s => s.Importance).ToList();

        foreach (var section in sortedSections)
        {
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine(section.Content);
            sb.AppendLine();
        }

        var result = sb.ToString();

        // 替换变量
        foreach (var (key, value) in _variables)
        {
            result = result.Replace($"{{{key}}}", value);
        }

        return result;
    }

    /// <summary>
    /// 构建为ChatMessage
    /// </summary>
    public ChatMessage BuildAsUserMessage()
    {
        return ChatMessage.User(Build());
    }

    /// <summary>
    /// 构建为系统消息和用户消息
    /// </summary>
    public (ChatMessage System, ChatMessage User) BuildAsMessages()
    {
        return (
            ChatMessage.System(_systemPrompt ?? "You are a helpful assistant."),
            ChatMessage.User(Build())
        );
    }
}

internal sealed record Section(string Title, string Content, int Importance);
