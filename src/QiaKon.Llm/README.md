# QiaKon.Llm

大模型核心接口与类型定义。

## 核心组件

### 消息模型

- `ChatMessage` - 聊天消息，支持 System/User/Assistant/Tool 四种角色
- `ContentBlock` - 内容块基类，包含：
  - `TextContentBlock` - 文本内容
  - `ImageContentBlock` - 图片内容
  - `ToolCallContentBlock` - 工具调用
  - `ToolResultContentBlock` - 工具结果

### 客户端接口

```csharp
public interface ILlmClient
{
    LlmProviderType Provider { get; }
    string Model { get; }
    Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken ct = default);
    IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(ChatCompletionRequest request, CancellationToken ct = default);
    ValueTask DisposeAsync();
}
```

### 配置选项

```csharp
public sealed record LlmOptions
{
    public required LlmProviderType Provider { get; init; }  // OpenAI / Anthropic
    public required string Model { get; init; }
    public required string BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public string? Organization { get; init; }
    public int MaxConcurrency { get; init; } = 5;
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;
    public LlmInferenceOptions InferenceOptions { get; init; } = new();
}
```

### 推理参数

```csharp
public sealed record LlmInferenceOptions
{
    public int? MaxTokens { get; init; }
    public double Temperature { get; init; } = 0.7;
    public double TopP { get; init; } = 1.0;
    public IReadOnlyList<string>? StopSequences { get; init; }
    public bool Stream { get; init; } = false;
}
```

### 生命周期管理

`ManagedLlmClient` 封装了并发控制和资源释放：

```csharp
using var client = factory.CreateManagedClient(options);
var response = await client.CompleteAsync(request);
```

### Agent 框架

```csharp
// Agent 请求
var request = new AgentRequest
{
    UserInput = "查询北京天气",
    MaxTurns = 10,
    Tools = [myTool]
};

// 执行 Agent
var response = await agent.ExecuteAsync(request);
Console.WriteLine(response.Response);
```

### 重试策略

内置指数退避重试：

```csharp
// 默认策略：500ms * 2^retryCount + jitter，最大30s
var strategy = LlmRetryStrategy.ExponentialBackoff();

// 线性退避
var linear = LlmRetryStrategy.LinearBackoff(delayMs: 1000);
```

## 向后兼容

`LegacyAliases.cs` 提供旧版接口兼容：

- `ILLMProvider` - 旧版提供者接口
- `LLMProviderConfig` - 旧版配置
- `LLMProviderFactory` - 旧版工厂
