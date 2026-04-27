# QiaKon.Retrieval.Embedding

文本嵌入（Embedding）抽象层，定义将文本转换为向量表示的通用接口。

## 定位

本模块是 RAG 系统的**向量化契约层**，不绑定任何具体的 Embedding Provider（如 OpenAI、Ollama、本地模型等）。

## 核心接口

```csharp
public interface IEmbeddingService
{
    int Dimensions { get; }           // 嵌入向量维度
    string ModelName { get; }         // 模型名称

    Task<ReadOnlyMemory<float>> EmbedAsync(string text);
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(IEnumerable<string> texts);
}
```

## 使用方式

实现 `IEmbeddingService` 并注册到 DI：

```csharp
// 示例：基于 OpenAI 的 Embedding 实现
public class OpenAIEmbeddingService : IEmbeddingService
{
    public int Dimensions => 1536;
    public string ModelName => "text-embedding-3-small";

    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text)
    {
        // 调用 OpenAI Embedding API
    }
}

// 注册
services.AddSingleton<IEmbeddingService, OpenAIEmbeddingService>();
```

## 注意事项

- `Dimensions` 必须与向量数据库集合创建时的维度一致
- `EmbedBatchAsync` 通常比多次调用 `EmbedAsync` 效率更高
- 建议对 Embedding 结果做本地缓存，避免重复计算
