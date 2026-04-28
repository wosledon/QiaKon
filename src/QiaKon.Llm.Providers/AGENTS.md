# QiaKon.Llm.Providers - AGENTS.md

> **模块**: LLM 厂商适配  
> **职责**: 多厂商 LLM API 适配、重试策略、流式输出  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Llm.Tokenization`  
> **被依赖**: `QiaKon.Llm`, `QiaKon.Api`

---

## 一、模块职责

本模块提供多厂商 LLM API 的统一适配层，支持 OpenAI、Azure OpenAI、Anthropic 等厂商。

**核心职责**:
- `ILlmClient` 接口实现
- 多厂商 API 适配
- 重试策略与错误处理
- 流式输出支持
- 工具调用支持

---

## 二、核心接口

### 2.1 ILlmClient

```csharp
public interface ILlmClient
{
    string Model { get; }
    
    Task<ChatCompletion> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);
        
    IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);
}
```

### 2.2 LlmClientFactory

```csharp
public interface ILlmClientFactory
{
    ILlmClient CreateClient(string provider, string model);
}
```

---

## 三、已实现厂商

### 3.1 OpenAI

```csharp
public class OpenAIClient : ILlmClient
{
    private readonly OpenAIClientOptions _options;
    private readonly HttpClient _httpClient;
    
    // 实现 OpenAI API 调用
}
```

**配置**:
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4",
    "BaseUrl": "https://api.openai.com/v1"
  }
}
```

### 3.2 Anthropic

```csharp
public class AnthropicClient : ILlmClient
{
    // 实现 Anthropic Claude API 调用
}
```

### 3.3 LlmProviderAdapter

通用适配器，支持自定义 BaseUrl 的 OpenAI 兼容 API。

---

## 四、重试策略

```csharp
public record LlmRetryStrategy
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
    public BackoffType Backoff { get; init; } = BackoffType.Exponential;
}
```

---

## 五、开发规范

### 5.1 添加新厂商

1. 实现 `ILlmClient` 接口
2. 处理厂商特定的请求/响应格式
3. 实现流式输出
4. 实现工具调用（如支持）
5. 编写集成测试

### 5.2 错误处理

- **RateLimit**: 429 状态码，使用指数退避重试
- **Timeout**: 请求超时，抛出 `TimeoutException`
- **AuthError**: 401 状态码，检查 API Key
- **InvalidRequest**: 400 状态码，验证请求参数

### 5.3 流式输出

```csharp
await foreach (var chunk in client.CompleteStreamAsync(request))
{
    await response.WriteAsync(chunk.Content);
    await response.Body.FlushAsync();
}
```

---

## 六、测试要求

- 各厂商 API 调用（使用 Mock）
- 重试策略验证
- 流式输出正确性
- 工具调用流程

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
