using System.Text;

namespace QiaKon.Llm.Prompt;

/// <summary>
/// Prompt模板
/// </summary>
public sealed class PromptTemplate
{
    private readonly string _template;
    private readonly List<string> _requiredVariables;
    private readonly List<string> _optionalVariables;

    public PromptTemplate(string template)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
        _requiredVariables = ParseVariables(template, required: true);
        _optionalVariables = ParseVariables(template, required: false);
    }

    /// <summary>
    /// 模板字符串
    /// </summary>
    public string Template => _template;

    /// <summary>
    /// 必需变量列表
    /// </summary>
    public IReadOnlyList<string> RequiredVariables => _requiredVariables.AsReadOnly();

    /// <summary>
    /// 可选变量列表
    /// </summary>
    public IReadOnlyList<string> OptionalVariables => _optionalVariables.AsReadOnly();

    /// <summary>
    /// 渲染Prompt
    /// </summary>
    public string Render(IDictionary<string, string> variables)
    {
        // 检查必需变量
        var missing = _requiredVariables.Where(v => !variables.ContainsKey(v)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"缺少必需变量: {string.Join(", ", missing)}");
        }

        var result = _template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{key}}}", value);
        }

        // 移除未提供的可选变量占位符
        foreach (var optVar in _optionalVariables)
        {
            if (!variables.ContainsKey(optVar))
            {
                result = result.Replace($"{{{optVar}}}", "");
            }
        }

        return result;
    }

    /// <summary>
    /// 渲染Prompt（使用对象属性作为变量）
    /// </summary>
    public string RenderFromObject(object obj)
    {
        var variables = ObjectToDictionary(obj);
        return Render(variables);
    }

    /// <summary>
    /// 创建渲染器构建器
    /// </summary>
    public PromptRendererBuilder CreateBuilder()
    {
        return new PromptRendererBuilder(this);
    }

    private static List<string> ParseVariables(string template, bool required)
    {
        var variables = new List<string>();
        var span = template.AsSpan();
        var start = 0;

        while (start < span.Length)
        {
            var openBrace = span[start..].IndexOf('{');
            if (openBrace < 0)
                break;

            var closeBrace = span[(start + openBrace + 1)..].IndexOf('}');
            if (closeBrace < 0)
                break;

            var varName = span[(start + openBrace + 1)..(start + openBrace + 1 + closeBrace)].ToString();

            // 必需变量: {name}，可选变量: {?name}
            var isRequired = !varName.StartsWith('?');
            var cleanName = isRequired ? varName : varName[1..];

            if (isRequired == required)
            {
                variables.Add(cleanName);
            }

            start = start + openBrace + 1 + closeBrace + 1;
        }

        return variables;
    }

    private static Dictionary<string, string> ObjectToDictionary(object obj)
    {
        var dict = new Dictionary<string, string>();
        var properties = obj.GetType().GetProperties();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            dict[prop.Name] = value?.ToString() ?? "";
        }

        return dict;
    }
}

/// <summary>
/// Prompt渲染器构建器
/// </summary>
public sealed class PromptRendererBuilder
{
    private readonly PromptTemplate _template;
    private readonly Dictionary<string, string> _variables = new();

    internal PromptRendererBuilder(PromptTemplate template)
    {
        _template = template;
    }

    public PromptRendererBuilder WithVariable(string name, string value)
    {
        _variables[name] = value;
        return this;
    }

    public PromptRendererBuilder WithCondition(string name, bool condition, string trueValue, string? falseValue = null)
    {
        _variables[name] = condition ? trueValue : (falseValue ?? "");
        return this;
    }

    public PromptRendererBuilder WithList(string name, IEnumerable<string> items, string separator = "\n")
    {
        _variables[name] = string.Join(separator, items);
        return this;
    }

    public string Build()
    {
        return _template.Render(_variables);
    }
}
