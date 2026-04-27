# QiaKon LLM 库

大语言模型集成库，提供上下文工程、Prompt框架和多云Provider支持。

## 项目结构

```
QiaKon.Llm/                    # 核心抽象层
├── Models/                    # 数据模型
│   ├── ChatMessage.cs        # 消息模型
│   ├── ChatCompletionRequest.cs  # 请求模型
│   ├── ChatCompletionResponse.cs # 响应模型
│   └── ToolDefinition.cs     # 工具定义
├── Providers/                 # Provider接口和配置
│   ├── ILLMProvider.cs       # LLM Provider接口
│   └── LLMProviderConfig.cs  # Provider配置（内含在ILLMProvider.cs）
├── LLMServiceCollectionExtensions.cs  # DI扩展
└── README.md                 # 本文档

QiaKon.Llm.Context/            # 上下文工程
├── ConversationContext.cs    # 对话上下文管理
├── MessageTrimmer.cs         # 消息裁剪策略
├── ContextTemplate.cs        # 上下文模板
└── ContextServiceCollectionExtensions.cs

QiaKon.Llm.Prompt/             # Prompt框架
├── PromptTemplate.cs         # Prompt模板引擎
├── PromptChain.cs            # Prompt链
└── StructuredPromptBuilder.cs # 结构化Prompt构建器

QiaKon.Llm.Providers/          # Provider实现
├── BaseLLMProvider.cs        # Provider基类
├── OpenAIProvider.cs         # OpenAI兼容API实现
├── AnthropicProvider.cs      # Anthropic实现
├── LLMProviderFactory.cs     # Provider工厂
└── ProviderServiceCollectionExtensions.cs  # DI扩展
```

## 快速开始

### 1. 安装依赖

在你的项目中引用需要的库：

```xml
<ItemGroup>
  <ProjectReference Include="src\QiaKon.Llm\QiaKon.Llm.csproj" />
  <ProjectReference Include="src\QiaKon.Llm.Context\QiaKon.Llm.Context.csproj" />
  <ProjectReference Include="src\QiaKon.Llm.Prompt\QiaKon.Llm.Prompt.csproj" />
  <ProjectReference Include="src\QiaKon.Llm.Providers\QiaKon.Llm.Providers.csproj" />
</ItemGroup>
```

### 2. 配置依赖注入

#### 方式一：使用Provider工厂（推荐）

```csharp
using Microsoft.Extensions.DependencyInjection;
using QiaKon.Llm;
using QiaKon.Llm.Providers;

var services = new ServiceCollection();

// 自动识别Provider类型（根据URL）
services.AddLLMProvider(new LLMProviderConfig
{
    ApiKey = "sk-your-key",
    BaseUrl = "https://api.openai.com/v1",  // 或其他兼容OpenAI的端点
    DefaultModel = "gpt-4o"
}, isDefault: true);

// 或者使用快速方法
services.AddOpenAICompatibleProvider(
    apiKey: "sk-your-key",
    baseUrl: "https://api.openai.com/v1",
    defaultModel: "gpt-4o",
    isDefault: true);
```

#### 方式二：注册多个Provider

```csharp
var configs = new[]
{
    new LLMProviderConfig
    {
        Name = "openai",
        ApiKey = "sk-openai-key",
        BaseUrl = "https://api.openai.com/v1",
        DefaultModel = "gpt-4o",
        ProviderType = ProviderType.OpenAICompatible
    },
    new LLMProviderConfig
    {
        Name = "anthropic",
        ApiKey = "sk-ant-key",
        BaseUrl = "https://api.anthropic.com",
        DefaultModel = "claude-3-5-sonnet-20241022",
        ProviderType = ProviderType.Anthropic
    },
    new LLMProviderConfig
    {
        Name = "ollama",
        ApiKey = "not-needed",  // Ollama通常不需要API Key
        BaseUrl = "http://localhost:11434/v1",
        DefaultModel = "llama3",
        ProviderType = ProviderType.OpenAICompatible
    }
};

services.AddLLMProviders(configs, defaultProviderName: "openai");
```

#### 方式三：从环境变量创建

```csharp
// 设置环境变量：LLM_API_KEY, LLM_DEFAULT_MODEL
var provider = LLMProviderFactory.CreateFromEnvironment();

// 或者指定特定端点
var azureProvider = LLMProviderFactory.CreateFromEnvironment(
    baseUrl: "https://your-resource.openai.azure.com",
    providerType: ProviderType.OpenAICompatible);
```

#### 方式四：使用Provider工厂快速创建

```csharp
// OpenAI
var openai = LLMProviderFactory.CreateOpenAICompatible(
    apiKey: "sk-your-key",
    baseUrl: "https://api.openai.com/v1",
    defaultModel: "gpt-4o");

// Azure OpenAI
var azure = LLMProviderFactory.CreateOpenAICompatible(
    apiKey: "azure-key",
    baseUrl: "https://your-resource.openai.azure.com",
    defaultModel: "gpt-4o",
    name: "Azure OpenAI");

// Ollama（本地）
var ollama = LLMProviderFactory.CreateOpenAICompatible(
    apiKey: "not-needed",
    baseUrl: "http://localhost:11434/v1",
    defaultModel: "llama3",
    name: "Ollama");

// LocalAI
var localai = LLMProviderFactory.CreateOpenAICompatible(
    apiKey: "your-key",
    baseUrl: "http://localhost:8080/v1",
    defaultModel: "mistral",
    name: "LocalAI");

// Anthropic
var anthropic = LLMProviderFactory.CreateAnthropic(
    apiKey: "sk-ant-your-key",
    defaultModel: "claude-3-5-sonnet-20241022");
```

## 常见供应商配置

### OpenAI

```csharp
services.AddOpenAICompatibleProvider(
    apiKey: "sk-your-openai-key",
    baseUrl: "https://api.openai.com/v1",
    defaultModel: "gpt-4o",
    isDefault: true);
```

### Azure OpenAI

```csharp
services.AddOpenAICompatibleProvider(
    apiKey: "azure-api-key",
    baseUrl: "https://your-resource.openai.azure.com/openai",
    defaultModel: "gpt-4o",
    name: "Azure OpenAI",
    isDefault: true);
```

### Ollama（本地）

```csharp
services.AddOpenAICompatibleProvider(
    apiKey: "not-needed",  // Ollama通常不需要Key
    baseUrl: "http://localhost:11434/v1",
    defaultModel: "llama3",
    name: "Ollama");
```

### LocalAI

```csharp
services.AddOpenAICompatibleProvider(
    apiKey: "your-localai-key",
    baseUrl: "http://localhost:8080/v1",
    defaultModel: "mistral",
    name: "LocalAI");
```

### Claude（Anthropic）

```csharp
services.AddAnthropicProvider(new LLMProviderConfig
{
    ApiKey = "sk-ant-your-key",
    DefaultModel = "claude-3-5-sonnet-20241022"
}, isDefault: true);
```

### 自定义Headers（某些供应商需要）

```csharp
services.AddOpenAICompatibleProvider(
    apiKey: "your-key",
    baseUrl: "https://custom-provider.com/v1",
    defaultModel: "custom-model",
    customHeaders: new Dictionary<string, string>
    {
        ["X-Custom-Header"] = "custom-value",
        ["X-Org-ID"] = "org-123"
    });
```

### 3. 基本对话

```csharp
var provider = serviceProvider.GetRequiredService<ILLMProvider>();

var response = await provider.CompleteAsync(new ChatCompletionRequest
{
    Model = "gpt-4o",
    Messages = new[]
    {
        ChatMessage.System("你是一个有用的助手"),
        ChatMessage.User("请解释什么是依赖注入")
    },
    Temperature = 0.7,
    MaxTokens = 500
});

Console.WriteLine(response.Message.GetTextContent());
Console.WriteLine($"Token使用: {response.Usage?.TotalTokens}");
```

### 4. 流式响应

```csharp
var request = new ChatCompletionRequest
{
    Model = "claude-3-5-sonnet-20241022",
    Messages = new[] { ChatMessage.User("写一首关于编程的诗") },
    Stream = true
};

await foreach (var streamEvent in provider.CompleteStreamingAsync(request))
{
    if (streamEvent.IsDone)
    {
        Console.WriteLine("\n[完成]");
    }
    else if (!string.IsNullOrEmpty(streamEvent.DeltaText))
    {
        Console.Write(streamEvent.DeltaText);
    }
}
```

### 5. 上下文管理

```csharp
var registry = serviceProvider.GetRequiredService<ContextTemplateRegistry>();
var template = registry.Get("assistant");

var context = template?.CreateContext(new Dictionary<string, string>
{
    ["role"] = "技术顾问"
});

// 添加对话历史
context?.AddMessage(ChatMessage.User("如何设计微服务？"));
context?.AddMessage(ChatMessage.Assistant("需要考虑服务拆分..."));

Console.WriteLine($"消息数: {context?.Count}");
Console.WriteLine($"估算Token: {context?.EstimateTokenCount()}");
```

### 6. Prompt模板

```csharp
var template = new PromptTemplate("""
    你是一个专业的{role}。
    任务: {task}
    要求: {requirements}
    
    请提供详细方案。
    """);

var prompt = template.CreateBuilder()
    .WithVariable("role", "架构师")
    .WithVariable("task", "设计电商系统")
    .WithVariable("requirements", "高并发、可扩展")
    .Build();
```

### 7. 工具调用

```csharp
var weatherTool = new ToolDefinition
{
    Name = "get_weather",
    Description = "获取城市天气",
    ParametersJsonSchema = """
        {
            "type": "object",
            "properties": {
                "city": { "type": "string" }
            },
            "required": ["city"]
        }
        """
};

var response = await provider.CompleteAsync(new ChatCompletionRequest
{
    Model = "gpt-4o",
    Messages = new[] { ChatMessage.User("北京天气如何？") },
    Tools = new[] { weatherTool },
    ToolChoice = "auto"
});

if (response.HasToolCalls)
{
    var toolCalls = response.GetToolCalls();
    foreach (var toolCall in toolCalls)
    {
        Console.WriteLine($"调用: {toolCall.Name}");
        Console.WriteLine($"参数: {toolCall.ArgumentsJson}");
    }
}
```

### 8. 结构化Prompt

```csharp
var (system, user) = new StructuredPromptBuilder()
    .WithSystemPrompt("你是代码审查专家")
    .AddSection("待审查代码", "public class UserService { ... }")
    .AddConstraint("检查空值处理")
    .AddConstraint("检查异常处理")
    .BuildAsMessages();

var response = await provider.CompleteAsync(new ChatCompletionRequest
{
    Model = "gpt-4o",
    Messages = new[] { system, user }
});
```

## 核心特性

### 上下文工程 (QiaKon.Llm.Context)

- **对话历史管理**: 自动维护消息列表
- **Token限制**: 基于Token数自动裁剪历史
- **上下文模板**: 可复用的对话模式
- **消息裁剪策略**: 支持自定义裁剪逻辑

### Prompt框架 (QiaKon.Llm.Prompt)

- **模板引擎**: 支持变量替换和条件渲染
- **Prompt链**: 组合多个模板形成工作流
- **结构化构建**: 章节化组织Prompt内容
- **示例和约束**: 内置Few-shot支持

### Provider驱动 (QiaKon.Llm.Providers)

- **OpenAI兼容API**: 支持OpenAI、Azure OpenAI、Ollama、LocalAI等
- **Anthropic**: 支持Claude 3系列模型
- **Provider工厂**: 根据配置自动创建合适的Provider
- **自动识别**: 根据URL自动检测供应商类型
- **统一接口**: 相同的API调用不同Provider
- **流式响应**: 支持SSE流式输出
- **工具调用**: 统一的Function Calling接口
- **灵活配置**: 支持自定义Headers、API版本等

### 9. Provider自动识别

```csharp
// 根据URL自动识别Provider类型
var config = new LLMProviderConfig
{
    ApiKey = "your-key",
    BaseUrl = "http://localhost:11434/v1"  // 自动识别为Ollama
};

var provider = LLMProviderFactory.CreateProvider(config);
Console.WriteLine(provider.ProviderName);  // 输出: Ollama
```

### 10. 多Provider切换

```csharp
// 注册多个Provider
services.AddLLMProviders(new[]
{
    new LLMProviderConfig { Name = "openai", ... },
    new LLMProviderConfig { Name = "anthropic", ... },
    new LLMProviderConfig { Name = "ollama", ... }
}, defaultProviderName: "openai");

// 使用默认Provider
var defaultProvider = serviceProvider.GetRequiredService<ILLMProvider>();

// 使用特定Provider
var anthropicProvider = serviceProvider.GetKeyedService<ILLMProvider>("anthropic");
var ollamaProvider = serviceProvider.GetKeyedService<ILLMProvider>("ollama");
```

### 11. Provider配置增强

```csharp
// 增强配置支持更多选项
var config = new LLMProviderConfig
{
    Name = "my-provider",
    ApiKey = "your-key",
    BaseUrl = "https://api.example.com/v1",
    DefaultModel = "model-name",
    ProviderType = ProviderType.OpenAICompatible,
    TimeoutSeconds = 120,
    MaxRetries = 3,
    ApiVersion = "2024-01-01",  // 某些供应商需要
    CustomHeaders = new Dictionary<string, string>
    {
        ["X-Custom-Header"] = "value"
    }
};

// 配置验证
if (config.Validate(out var errorMessage))
{
    var provider = LLMProviderFactory.CreateProvider(config);
}
else
{
    Console.WriteLine($"配置无效: {errorMessage}");
}
```

## 架构设计原则

1. **接口优先**: 定义清晰的抽象接口
2. **依赖注入**: 完全支持DI容器
3. **可扩展**: 易于添加新Provider
4. **类型安全**: 使用C# 10+ record类型
5. **异步优先**: 全面支持async/await

## 扩展指南

### 添加新的Provider

```csharp
public class MyProvider : BaseLLMProvider
{
    public override string ProviderName => "MyProvider";
    
    public override Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // 实现调用逻辑
    }
    
    // 实现其他方法...
}

// 注册
services.AddSingleton<ILLMProvider>(sp => 
    new MyProvider(config));
```

### 自定义消息裁剪器

```csharp
public class MyTrimmer : IMessageTrimmer
{
    public void TrimToTokenLimit(List<ChatMessage> messages, int maxTokens)
    {
        // 自定义裁剪逻辑
    }
}
```

## 依赖项

- .NET 10.0
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Http (Providers项目)

## 许可证

MIT
