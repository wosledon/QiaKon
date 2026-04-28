# QiaKon.Llm.Context - AGENTS.md

> **模块**: LLM 上下文管理  
> **职责**: 对话上下文管理、消息裁剪、上下文模板  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Llm.Tokenization`  
> **被依赖**: `QiaKon.Llm`, `QiaKon.Llm.Providers`

---

## 一、模块职责

本模块负责管理 LLM 对话的上下文，包括消息的添加、删除、裁剪和 Token 估算。

**核心职责**:
- `ConversationContext`: 对话上下文管理
- `ContextTemplate`: 可复用的上下文模板
- `IMessageTrimmer`: 消息裁剪策略
- Token 估算与上下文长度控制

---

## 二、核心类

### 2.1 ConversationContext

```csharp
public sealed class ConversationContext
{
    public ConversationContext(
        int? maxMessages = null,
        int? maxTokens = null,
        IMessageTrimmer? trimmer = null);
        
    public int Count { get; }
    
    public void AddMessage(ChatMessage message);
    public void AddMessages(IEnumerable<ChatMessage> messages);
    public IReadOnlyList<ChatMessage> GetMessages();
    public void Clear();
    public ChatMessage? RemoveLast();
    public void SetSystemPrompt(string systemPrompt);
    public int EstimateTokenCount(Func<string, int>? tokenCounter = null);
}
```

### 2.2 ContextTemplate

```csharp
public sealed record ContextTemplate
{
    public required string Name { get; init; }
    public string? SystemPromptTemplate { get; init; }
    public IReadOnlyList<ChatMessage>? InitialMessages { get; init; }
    public int? MaxMessages { get; init; }
    public int? MaxTokens { get; init; }
    
    public ConversationContext CreateContext(
        IDictionary<string, string>? variables = null,
        IMessageTrimmer? trimmer = null);
}
```

### 2.3 IMessageTrimmer

```csharp
public interface IMessageTrimmer
{
    (IList<ChatMessage> kept, IList<ChatMessage> removed) Trim(
        IList<ChatMessage> messages,
        int maxMessages,
        int maxTokens,
        Func<string, int> tokenCounter);
}
```

---

## 三、消息裁剪策略

### 3.1 DefaultMessageTrimmer

保留最近 N 条消息，删除旧消息。

### 3.2 PriorityMessageTrimmer

保留高优先级消息（系统消息、用户明确指定的消息）。

### 3.3 SummaryMessageTrimmer

将旧消息压缩为摘要，保留关键信息。

---

## 四、开发规范

### 4.1 上下文管理

- 使用 `AddMessage` 添加消息
- 使用 `EstimateTokenCount` 估算 Token 数量
- 超过限制时自动调用 `MessageTrimmer`

### 4.2 模板使用

```csharp
var template = new ContextTemplate
{
    Name = "qa-assistant",
    SystemPromptTemplate = "你是{{domain}}领域的专家。",
    MaxMessages = 20,
    MaxTokens = 8000
};

var context = template.CreateContext(
    variables: new Dictionary<string, string> { ["domain"] = "技术" });
```

---

## 五、测试要求

- 上下文添加和删除逻辑
- 消息裁剪策略正确性
- Token 估算准确性
- 模板变量替换

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
