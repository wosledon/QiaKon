# QiaKon.Llm.Providers

OpenAI / Anthropic 大模型驱动实现。

## 支持的供应商

| 供应商      | 类型枚举                    | 默认 Endpoint               |
| ----------- | --------------------------- | --------------------------- |
| OpenAI 兼容 | `LlmProviderType.OpenAI`    | `https://api.openai.com/v1` |
| Anthropic   | `LlmProviderType.Anthropic` | `https://api.anthropic.com` |

## 快速开始

### 方式一：静态便捷方法（推荐）

```csharp
// OpenAI
var client = LlmClients.OpenAI(
    model: "gpt-4o",
    baseUrl: "https://api.openai.com/v1",
    apiKey: "sk-...",
    builder => builder
        .WithTemperature(0.7)
        .WithMaxTokens(1024));

// Anthropic
var client = LlmClients.Anthropic(
    model: "claude-sonnet-4-20250514",
    baseUrl: "https://api.anthropic.com",
    apiKey: "sk-ant-...");

// 发送请求
var response = await client.CompleteAsync(new ChatCompletionRequest
{
    Model = "gpt-4o",
    Messages = new[]
    {
        ChatMessage.System("你是一个助手"),
        ChatMessage.User("你好")
    }
});

Console.WriteLine(response.Message.GetTextContent());
```

### 方式二：配置对象 + 工厂

```csharp
var options = new LlmOptions
{
    Provider = LlmProviderType.OpenAI,
    Model = "gpt-4o",
    BaseUrl = "https://api.openai.com/v1",
    ApiKey = "sk-...",
    MaxConcurrency = 5,
    MaxRetries = 3,
    InferenceOptions = new LlmInferenceOptions
    {
        Temperature = 0.7,
        MaxTokens = 2048
    }
};

var factory = new LlmClientFactory();
var client = factory.CreateClient(options);
```

### 方式三：带生命周期管理的客户端

```csharp
var options = new LlmOptions { /* ... */ };

using var managed = factory.CreateManagedClient(options);
// SemaphoreSlim 并发控制自动生效

var response = await managed.CompleteAsync(request);
```

## LlmOptionsBuilder

```csharp
var client = LlmClients.OpenAI("gpt-4o", "https://api.openai.com/v1", "sk-...")
    .WithMaxConcurrency(10)           // 最大并发数
    .WithTimeout(60)                   // 超时秒数
    .WithMaxRetries(5)                // 最大重试次数
    .WithTemperature(0.8)             // 温度
    .WithMaxTokens(4096)              // 最大 Token
    .WithTopP(0.95)                  // Top-P
    .WithStopSequences("###", "END")  // 停止序列
    .EnableDetailedLogging()          // 详细日志
    .WithOrganization("org-xxx");    // OpenAI 组织
```

## 流式输出

```csharp
var request = new ChatCompletionRequest
{
    Model = "gpt-4o",
    Messages = new[] { ChatMessage.User("写一首诗") }
};

await foreach (var chunk in client.CompleteStreamAsync(request))
{
    Console.Write(chunk.Content);
    if (chunk.IsComplete) break;
}
```

## 工具调用（Function Calling）

```csharp
var tool = new LlmTool
{
    Name = "get_weather",
    Description = "获取城市天气",
    ParametersJsonSchema = """
        {
            "type": "object",
            "properties": {
                "city": {"type": "string", "description": "城市名"}
            },
            "required": ["city"]
        }
        """,
    Executor = async (name, args, ct) =>
    {
        // 解析参数并执行
        var result = await SomeWeatherApi(args);
        return new ToolExecutionResult
        {
            ToolName = name,
            ToolCallId = "xxx",
            Result = result
        };
    }
};

var request = new ChatCompletionRequest
{
    Model = "gpt-4o",
    Messages = new[] { ChatMessage.User("北京天气怎么样？") },
    Tools = new[] { tool }
};
```

## HTTP 重试处理器

内置指数退避重试，支持：

- `503 Service Unavailable`
- `429 Too Many Requests`
- `500 Internal Server Error`
- `502 Bad Gateway`
- `504 Gateway Timeout`

## 向后兼容

`LlmProviderAdapter` 将 `ILlmClient` 适配为旧版 `ILLMProvider` 接口：

```csharp
// 旧代码
ILLMProvider provider = LLMProviderFactory.CreateProvider(config);

// 新代码自动适配
ILLMProvider provider = new LlmProviderAdapter(new LlmClientFactory().CreateClient(options));
```
