# QiaKon.Llm.Context

对话上下文管理模块。

## 核心组件

### ConversationContext

对话上下文管理器，负责消息的添加、裁剪和上下文窗口维护。

```csharp
var context = new ConversationContext(
    maxMessages: 50,      // 最大消息数
    maxTokens: 4000,     // 最大 Token 数
    trimmer: new DefaultMessageTrimmer());

// 添加消息
context.AddMessage(ChatMessage.User("你好"));
context.AddMessage(ChatMessage.Assistant("有什么可以帮助你的？"));

// 获取所有消息
IReadOnlyList<ChatMessage> messages = context.GetMessages();

// 设置系统提示词
context.SetSystemPrompt("你是一个专业的助手");

// 清空历史
context.Clear();

// 移除最后一条
var removed = context.RemoveLast();

// 估算 Token 数
int tokens = context.EstimateTokenCount();
```

### ContextTemplate

可复用的上下文模板，支持变量替换。

```csharp
var template = new ContextTemplate
{
    Name = "code-review",
    SystemPromptTemplate = "你是一个代码审查员，审查语言：{language}",
    MaxMessages = 20,
    MaxTokens = 8000
};

// 使用变量创建上下文
var context = template.CreateContext(
    variables: new Dictionary<string, string> { ["language"] = "C#" });
```

### ContextTemplateRegistry

上下文模板注册表。

```csharp
var registry = new ContextTemplateRegistry();
registry.Register(template);

if (registry.TryGet("code-review", out var t))
{
    var ctx = t.CreateContext();
}
```

### IMessageTrimmer

消息裁剪器接口，自定义裁剪策略。

```csharp
public interface IMessageTrimmer
{
    void TrimToTokenLimit(List<ChatMessage> messages, int maxTokens);
}
```

默认实现 `DefaultMessageTrimmer` 从最早的非系统消息开始移除。

## DI 扩展

```csharp
services.AddContextTemplates(registry =>
{
    registry.Register(new ContextTemplate { Name = "default", MaxMessages = 50 });
});

services.AddConversationContext(maxMessages: 50, maxTokens: 4000);
```

## 注意事项

- `ConversationContext` **不是**线程安全的，单线程使用
- Token 估算是近似值（中文约每2字符1 token，英文约每4字符1 token）
- 系统消息始终保留，不会被裁剪
