# QiaKon.Llm.Prompt - AGENTS.md

> **模块**: Prompt 管理  
> **职责**: Prompt 模板管理、链式构建、结构化输出  
> **依赖**: `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Llm`, `QiaKon.Llm.Context`

---

## 一、模块职责

本模块负责管理 LLM 的 Prompt 模板，支持模板渲染、链式构建和结构化输出。

**核心职责**:
- `PromptTemplate`: Prompt 模板定义与渲染
- `PromptChain`: 多步骤 Prompt 链
- `StructuredPromptBuilder`: 结构化 Prompt 构建器

---

## 二、核心类

### 2.1 PromptTemplate

```csharp
public sealed class PromptTemplate
{
    public string Name { get; init; }
    public string Template { get; init; }
    
    public string Render(IDictionary<string, string> variables);
}
```

### 2.2 PromptChain

```csharp
public sealed class PromptChain
{
    public PromptChain AddSystem(string systemPrompt);
    public PromptChain AddUser(string userPrompt);
    public PromptChain AddAssistant(string assistantPrompt);
    public PromptChain AddContext(string context);
    public IReadOnlyList<ChatMessage> Build();
}
```

### 2.3 StructuredPromptBuilder

用于构建 JSON Schema 约束的结构化输出 Prompt。

```csharp
public sealed class StructuredPromptBuilder
{
    public StructuredPromptBuilder WithSchema(string jsonSchema);
    public StructuredPromptBuilder WithExample(object example);
    public string Build();
}
```

---

## 三、开发规范

### 3.1 模板变量

使用 `{{variable_name}}` 语法：

```csharp
var template = new PromptTemplate
{
    Name = "qa-template",
    Template = "请回答关于{{topic}}的问题：{{question}}"
};

var rendered = template.Render(new Dictionary<string, string>
{
    ["topic"] = "技术",
    ["question"] = "什么是微服务？"
});
```

### 3.2 链式构建

```csharp
var messages = new PromptChain()
    .AddSystem("你是一个专业的技术顾问。")
    .AddContext("用户背景：10年开发经验")
    .AddUser("请解释一下 DDD 的核心概念")
    .Build();
```

---

## 四、最佳实践

1. **模板分离**: 将 Prompt 模板与代码分离
2. **版本控制**: Prompt 模板变更需要版本管理
3. **测试**: 对复杂 Prompt 编写测试验证输出格式
4. **结构化输出**: 使用 JSON Schema 约束输出格式

---

## 五、测试要求

- 模板变量渲染
- Prompt 链构建
- 结构化输出格式验证

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
