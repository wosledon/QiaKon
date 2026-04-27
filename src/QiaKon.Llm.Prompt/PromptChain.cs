namespace QiaKon.Llm.Prompt;

/// <summary>
/// Prompt链（组合多个Prompt模板）
/// </summary>
public sealed class PromptChain
{
    private readonly List<PromptChainNode> _nodes = new();

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
