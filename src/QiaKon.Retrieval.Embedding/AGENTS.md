# QiaKon.Retrieval.Embedding - AGENTS.md

> **模块**: 向量嵌入生成  
> **职责**: 调用 LLM 生成文档块的向量表示  
> **依赖**: `QiaKon.Llm.Providers`, `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Retrieval.VectorStore`, `QiaKon.Retrieval.DocumentProcessor`

---

## 一、模块职责

本模块负责将文本内容转换为向量表示，用于向量相似度检索。

**核心职责**:
- `IEmbeddingService` 接口定义
- 嵌入生成与批处理
- 多模型支持
- 向量维度验证

---

## 二、核心接口

### 2.1 IEmbeddingService

```csharp
public interface IEmbeddingService
{
    string ModelName { get; }
    int Dimension { get; }
    
    Task<ReadOnlyMemory<float>> EmbedAsync(
        string text, 
        CancellationToken ct = default);
        
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts, 
        CancellationToken ct = default);
}
```

### 2.2 EmbeddingOptions

```csharp
public class EmbeddingOptions
{
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimension { get; set; } = 1536;
    public int BatchSize { get; set; } = 100;
}
```

---

## 三、实现要点

### 3.1 批处理

```csharp
public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
    IReadOnlyList<string> texts, 
    CancellationToken ct = default)
{
    var batches = texts.Chunk(_options.BatchSize);
    var results = new List<ReadOnlyMemory<float>>();
    
    foreach (var batch in batches)
    {
        var embeddings = await _client.CreateEmbeddingsAsync(batch, ct);
        results.AddRange(embeddings);
    }
    
    return results;
}
```

### 3.2 向量维度

- OpenAI `text-embedding-3-small`: 1536 维
- OpenAI `text-embedding-3-large`: 3072 维
- 本地模型：根据模型配置

---

## 四、开发规范

### 4.1 使用示例

```csharp
services.AddEmbedding(options =>
{
    options.Provider = "OpenAI";
    options.Model = "text-embedding-3-small";
    options.BatchSize = 50;
});
```

### 4.2 错误处理

- **维度不匹配**: 抛出 `DimensionMismatchException`
- **API 限流**: 使用指数退避重试
- **批量过大**: 自动分批处理

---

## 五、测试要求

- 向量生成正确性
- 批处理逻辑
- 维度验证
- 错误重试

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
