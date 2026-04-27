# QiaKon.Retrieval.Chunnking.MoE

MoE（Mixture of Experts）智能分块模块，利用大模型进行语义级文档分块。

## 核心设计

### 配置驱动模型

MoE 采用**配置驱动**设计，模型不是写死的，而是通过 `LLMProviderConfig` 灵活配置：

| 场景 | 配置方式 | Provider 来源 |
|------|---------|--------------|
| 复用已有 Provider | 不设置 ProviderConfig | DI 容器中已注册的 `ILLMProvider` |
| 独立配置 | 设置 `ProviderConfig` | MoE 根据配置自动创建新的 Provider 实例 |

### 与传统分块的区别

| 维度 | 传统分块 | MoE 分块 |
|------|---------|---------|
| 分块依据 | 字符数/段落/句子 | LLM 语义理解 |
| 语义完整性 | 可能切断语义 | 在主题边界切割 |
| 全模态支持 | 否（需先转文本） | 是（模型直接理解） |
| 速度 | 快 | 较慢（调用 LLM） |
| 成本 | 无 | 有（LLM Token 费用） |

### SkipDocumentProcessing 选项

MoE 模块最重要的特性之一是可控制是否跳过文档预处理：

- **SkipDocumentProcessing = false**（默认）：先由 `DocumentProcessor` 将文档转为 Markdown，再由 MoE 分块
- **SkipDocumentProcessing = true**：MoE 直接接收原始文件（PDF/图片等），利用多模态模型直接理解和分块

## 快速开始

### 方式一：独立配置（推荐，配置驱动）

无需预先注册 `ILLMProvider`，直接在 MoE 配置中传入 LLM 配置：

```csharp
services.AddMoEChunking(options =>
{
    options.ProviderConfig = new LLMProviderConfig
    {
        Name = "moe-chunker",
        ProviderType = ProviderType.OpenAICompatible,
        ApiKey = "sk-xxx",
        BaseUrl = "https://api.openai.com",
        DefaultModel = "gpt-4o-mini",
        TimeoutSeconds = 60
    };
    options.MaxChunkSize = 2000;
    options.Temperature = 0.1;
});
```

### 方式二：复用已有 Provider

如果系统中已经注册了 `ILLMProvider`，MoE 可以直接复用：

```csharp
// 先注册全局 LLM Provider
services.AddOpenAIProvider(config); // 或 AddAnthropicProvider 等

// MoE 复用该 Provider，只指定模型名称
services.AddMoEChunking(options =>
{
    options.ModelName = "gpt-4o-mini";  // 覆盖 Provider 的默认模型
    options.MaxChunkSize = 2000;
    options.Temperature = 0.1;
});
```

### 模型名称解析优先级

MoE 解析最终模型名称的顺序：

1. `MoEChunkingOptions.ModelName`（最高优先级）
2. `MoEChunkingOptions.ProviderConfig.DefaultModel`
3. 兜底默认值 `"gpt-4o-mini"`
