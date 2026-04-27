# QiaKon.Llm.Prompt

Prompt 模板与构建框架。

## 核心组件

### PromptTemplate

模板语法：`{name}` 必需变量，`{?name}` 可选变量。

```csharp
var template = new PromptTemplate("""
    总结以下文本：
    
    {?prefix}
    
    文本：{content}
    
    请用{?language}总结。
    """);

// 必需变量检查
string result = template.Render(new Dictionary<string, string>
{
    ["content"] = "要总结的内容..."
});

// 可选变量不提供则为空
string minimal = template.Render(new Dictionary<string, string>
{
    ["content"] = "内容"
}); // prefix 和 language 会被移除
```

### PromptChain

多个 Prompt 模板链式组合，后一个节点的输出可作为变量输入。

```csharp
var chain = new PromptChain()
    .AddNode(
        template: extractionTemplate,
        variables: new Dictionary<string, string> { ["input"] = rawText },
        outputVariable: "extracted")
    .AddNode(
        template: summaryTemplate,
        variables: new Dictionary<string, string> { ["data"] = "{extracted}" })
    .AddConditionalNode(
        condition: ctx => ctx["has-errors"] == "false",
        template: finalTemplate);

string result = chain.Execute(initialVariables);
```

### StructuredPromptBuilder

结构化 Prompt 构建器，适合构建复杂的提示词。

```csharp
var prompt = new StructuredPromptBuilder()
    .WithSystemPrompt("你是一个技术写作专家")
    .AddSection("背景", "用户需要了解微服务的优势", importance: 2)
    .AddSection("要求", "- 简洁明了\n- 包含代码示例", importance: 1)
    .AddExample("微服务是什么？", "微服务是一种架构风格...")
    .AddConstraint("输出格式为 Markdown")
    .WithVariable("topic", "微服务")
    .Build();
```

### PromptRendererBuilder

流式构建 Prompt。

```csharp
string result = template.CreateBuilder()
    .WithVariable("name", "World")
    .WithCondition("showExtra", true, "额外信息", "基本内容")
    .WithList("items", new[] { "A", "B", "C" }, separator: ", ")
    .Build();
```

## 模板变量语法

| 语法      | 类型 | 说明                   |
| --------- | ---- | ---------------------- |
| `{name}`  | 必需 | 必须提供，否则抛出异常 |
| `{?name}` | 可选 | 不提供时自动移除占位符 |

## 与 ChatMessage 集成

```csharp
// 构建为用户消息
ChatMessage userMsg = structuredBuilder.BuildAsUserMessage();

// 构建为系统+用户消息对
var (system, user) = structuredBuilder.BuildAsMessages();
```
