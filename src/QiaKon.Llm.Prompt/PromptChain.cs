namespace QiaKon.Llm.Prompt;

/// <summary>
/// Prompt链（组合多个Prompt模板）
/// </summary>
public sealed class PromptChain
{
    private readonly List<PromptChainNode> _nodes = new();
    private string? _systemPrompt;
    private readonly List<ChatMessage> _messages = new();

    /// <summary>
    /// 添加系统提示词
    /// </summary>
    public PromptChain AddSystem(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    /// <summary>
    /// 添加用户消息
    /// </summary>
    public PromptChain AddUser(string userPrompt)
    {
        _messages.Add(ChatMessage.User(userPrompt));
        return this;
    }

    /// <summary>
    /// 添加助手消息
    /// </summary>
    public PromptChain AddAssistant(string assistantPrompt)
    {
        _messages.Add(ChatMessage.Assistant(assistantPrompt));
        return this;
    }

    /// <summary>
    /// 添加上下文内容
    /// </summary>
    public PromptChain AddContext(string context)
    {
        _messages.Add(ChatMessage.System(context));
        return this;
    }

    /// <summary>
    /// 添加Prompt节点
    /// </summary>
    public PromptChain AddNode(
        PromptTemplate template,
        IDictionary<string, string>? variables = null,
        string? outputVariable = null)
    {
        _nodes.Add(new PromptChainNode(template, variables, outputVariable));
        return this;
    }

    /// <summary>
    /// 添加条件节点
    /// </summary>
    public PromptChain AddConditionalNode(
        Func<IDictionary<string, string>, bool> condition,
        PromptTemplate template,
        IDictionary<string, string>? variables = null,
        string? outputVariable = null)
    {
        _nodes.Add(new PromptChainNode(template, variables, outputVariable, condition));
        return this;
    }

    /// <summary>
    /// 构建消息列表
    /// </summary>
    public IReadOnlyList<ChatMessage> Build()
    {
        var result = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(_systemPrompt))
        {
            result.Add(ChatMessage.System(_systemPrompt));
        }

        result.AddRange(_messages);

        return result.AsReadOnly();
    }

    /// <summary>
    /// 执行链并获取最终结果
    /// </summary>
    public string Execute(IDictionary<string, string> initialVariables)
    {
        var context = new Dictionary<string, string>(initialVariables);

        foreach (var node in _nodes)
        {
            // 检查条件
            if (node.Condition != null && !node.Condition(context))
            {
                continue;
            }

            // 合并变量
            var nodeVariables = new Dictionary<string, string>(context);
            if (node.Variables != null)
            {
                foreach (var (key, value) in node.Variables)
                {
                    nodeVariables[key] = value;
                }
            }

            // 渲染
            var rendered = node.Template.Render(nodeVariables);

            // 如果有输出变量名，存储结果
            if (!string.IsNullOrEmpty(node.OutputVariable))
            {
                context[node.OutputVariable] = rendered;
            }
        }

        // 返回最后一个节点的输出
        var lastNode = _nodes.LastOrDefault(n => n.OutputVariable != null || n.Condition == null);
        if (lastNode == null)
            return string.Empty;

        var lastVariables = new Dictionary<string, string>(context);
        if (lastNode.Variables != null)
        {
            foreach (var (key, value) in lastNode.Variables)
            {
                lastVariables[key] = value;
            }
        }

        return lastNode.Template.Render(lastVariables);
    }
}

internal sealed record PromptChainNode(
    PromptTemplate Template,
    IDictionary<string, string>? Variables,
    string? OutputVariable,
    Func<IDictionary<string, string>, bool>? Condition = null);
