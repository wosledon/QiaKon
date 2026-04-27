# QiaKon.Retrieval.Embedding

文本嵌入（Embedding）抽象层，定义将文本转换为向量表示的通用接口。

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

## 本地 ONNX 嵌入服务

`LocalEmbeddingService` 基于 ONNX Runtime 加载本地模型进行推理：

```csharp
// 注册（ModelPath 指向包含 onnx 模型和 tokenizer 文件的文件夹）
services.AddLocalEmbedding(options =>
{
    options.Dimensions = 1024;  // Qwen3-Embedding 输出维度
    options.ModelPath = @"C:\models\Qwen3-Embedding";  // 包含 model.onnx, vocab.json 等
    options.ModelName = "Qwen3-Embedding";
    options.MaxSequenceLength = 8192;
});

// 使用
public class MyService(IEmbeddingService embeddingService)
{
    public async Task EmbedText(string text)
    {
        var vector = await embeddingService.EmbedAsync(text);
    }
}
```

## 注意事项

- `Dimensions` 必须与向量数据库集合创建时的维度一致
- `EmbedBatchAsync` 通常比多次调用 `EmbedAsync` 效率更高
- 本地嵌入适合离线或对成本敏感的场景
