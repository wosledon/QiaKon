# QiaKon.Retrieval.Chunnking.MoE

MoE（Mixture of Experts）智能分块模块，利用大模型进行语义级文档分块。

## 核心设计

### 工厂模式 + 依赖注入

MoE 采用工厂模式管理 LLM 客户端生命周期，通过 DI 注册 `IMoEChunkingStrategyFactory` 单例：

```csharp
// 注册工厂（单例）
services.AddMoEChunking();

// 注册选项
services.AddMoEChunkingOptions(options =>
{
    options.MaxChunkSize = 2000;
});
```

### 与传统分块的区别

| 维度       | 传统分块         | MoE 分块             |
| ---------- | ---------------- | -------------------- |
| 分块依据   | 字符数/段落/句子 | LLM 语义理解         |
| 语义完整性 | 可能切断语义     | 在主题边界切割       |
| 全模态支持 | 否（需先转文本） | 是（模型直接理解）   |
| 速度       | 快               | 较慢（调用 LLM）     |
| 成本       | 无               | 有（LLM Token 费用） |

### SkipDocumentProcessing 选项

MoE 模块最重要的特性之一是可控制是否跳过文档预处理：

- **SkipDocumentProcessing = false**（默认）：先由 `DocumentProcessor` 将文档转为 Markdown，再由 MoE 分块
- **SkipDocumentProcessing = true**：MoE 直接接收原始文件（PDF/图片等），利用多模态模型直接理解和分块

## 快速开始

### 注册服务

```csharp
services.AddMoEChunking();
services.AddMoEChunkingOptions(options =>
{
    options.MaxChunkSize = 2000;
    options.MinOverlapSize = 100;
    options.MaxConcurrency = 5;
});
```

### 使用 MoE 分块

```csharp
public class MyService(IMoEChunkingStrategyFactory factory)
{
    public async Task ChunkDocument(ILLMProvider provider, string content)
    {
        // 通过工厂创建分块策略（工厂管理 provider 生命周期）
        var strategy = factory.Create(provider, _options);
        
        // 执行分块
        var chunks = await strategy.ChunkAsync(documentId, content);
    }
}
```

### 直接使用 ILlmClient

如果调用方自己管理 ILlmClient 生命周期：

```csharp
var strategy = factory.Create(llmClient, options);
var chunks = await strategy.ChunkAsync(documentId, content);
```

### 支持的供应商

| 供应商      | 类型枚举                    |
| ----------- | --------------------------- |
| OpenAI 兼容 | `LlmProviderType.OpenAI`    |
| Anthropic   | `LlmProviderType.Anthropic` |

## 生命周期管理

- `IMoEChunkingStrategyFactory` 为单例，由 DI 容器管理
- 工厂内部缓存同一 provider 的适配器，实现复用
- 应用关闭时，调用 `DisposeAsync()` 释放所有托管的 provider
