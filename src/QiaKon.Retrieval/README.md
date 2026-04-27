# QiaKon.Retrieval

RAG（检索增强生成）核心抽象层，定义文档、分块、检索管道的通用接口与数据模型。

## 定位

本模块是 RAG 子系统的**契约层**，不依赖任何具体实现（如 Embedding Provider、向量数据库、LLM）。所有实现类库均基于本模块的接口进行扩展。

## 核心概念

| 概念     | 接口                     | 说明                                              |
| -------- | ------------------------ | ------------------------------------------------- |
| 文档     | `IDocument` / `Document` | 原始文档，包含标题、内容、来源、MIME 类型和元数据 |
| 分块     | `IChunk` / `Chunk`       | 从文档拆分出的语义片段，携带位置信息和元数据      |
| 分块策略 | `IChunkingStrategy`      | 定义如何将文档内容拆分为多个块                    |
| 检索管道 | `IRagPipeline`           | 定义从文档索引到向量检索的完整流程                |

## 模块依赖关系

```
QiaKon.Retrieval（本模块：契约层）
    ├── QiaKon.Retrieval.DocumentProcessor  → 文档转 Markdown
    ├── QiaKon.Retrieval.Chunnking          → 字符/段落分块
    ├── QiaKon.Retrieval.Chunnking.MoE      → LLM 语义分块
    ├── QiaKon.Retrieval.Embedding          → 文本向量化
    └── QiaKon.Retrieval.VectorStore        → 向量存储
```

## 快速开始

### 1. 注册 RAG 基础设施

```csharp
services.AddRagInfrastructure();
```

### 2. 选择并注册分块策略（三选一）

```csharp
// 方案 A：字符滑动窗口分块（最快）
services.AddCharacterChunking(options =>
{
    options.MaxChunkSize = 2000;
    options.OverlapSize = 200;
});

// 方案 B：段落分块（保持段落完整性）
services.AddParagraphChunking(options =>
{
    options.MaxChunkSize = 2000;
});

// 方案 C：MoE 智能分块（基于 LLM 语义理解，质量最高）
// 方式 C1：配置驱动，独立指定 LLM 配置
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
    options.SkipDocumentProcessing = false; // 设为 true 时 MoE 直接处理原始文件
});

// 方式 C2：复用已注册的 ILLMProvider，仅覆盖模型名称
services.AddMoEChunking(options =>
{
    options.ModelName = "gpt-4o-mini";
    options.MaxChunkSize = 2000;
});
```

### 3. 注册具体实现

```csharp
// Embedding（需实现 IEmbeddingService）
services.AddSingleton<IEmbeddingService, YourEmbeddingService>();

// 向量存储（以 PostgreSQL + pgvector 为例）
services.AddNpgsqlVectorStore(options =>
{
    options.ConnectionString = "Host=...;Database=...;Username=...;Password=...";
});
```

### 4. 使用 RAG 管道

```csharp
public class DocumentService
{
    private readonly IRagPipeline _ragPipeline;

    public DocumentService(IRagPipeline ragPipeline)
    {
        _ragPipeline = ragPipeline;
    }

    public async Task IndexDocumentAsync(string content, string title)
    {
        var document = new Document
        {
            Title = title,
            Content = content,
            MimeType = "text/plain"
        };

        var record = await _ragPipeline.IndexAsync(document);
        Console.WriteLine($"索引完成，共 {record.ChunkCount} 个块");
    }

    public async Task SearchAsync(string query)
    {
        var results = await _ragPipeline.RetrieveAsync(query, new RetrievalOptions
        {
            TopK = 5,
            MinSimilarity = 0.7f
        });

        foreach (var result in results)
        {
            Console.WriteLine($"[{result.Score:F3}] {result.Chunk.Text}");
        }
    }
}
```

## 配置选项

### RetrievalOptions

| 属性              | 默认值         | 说明                  |
| ----------------- | -------------- | --------------------- |
| `TopK`            | 5              | 返回最相似的结果数量  |
| `MinSimilarity`   | null           | 最小相似度阈值（0~1） |
| `IncludeDocument` | true           | 是否返回完整文档信息  |
| `DistanceMetric`  | CosineDistance | 距离度量方式          |

## 与其他模块协作

| 阶段      | 模块                                           | 职责                         |
| --------- | ---------------------------------------------- | ---------------------------- |
| 文档处理  | `QiaKon.Retrieval.DocumentProcessor`           | 将 PDF/Word 等转为 Markdown  |
| 分块      | `QiaKon.Retrieval.Chunnking` / `Chunnking.MoE` | 将文本拆分为语义块           |
| 向量化    | `QiaKon.Retrieval.Embedding`                   | 将文本转为向量嵌入           |
| 存储/检索 | `QiaKon.Retrieval.VectorStore`                 | 向量数据库的存储与相似度搜索 |

## 注意事项

- `IChunkingStrategy` 接口已迁移至本模块，避免 `Retrieval` ↔ `Chunnking` 之间的循环依赖
- `IRagPipeline` 实现位于本模块，依赖 `IChunkingStrategy`、`IEmbeddingService` 和 `IVectorStore`
- 如需自定义分块策略，实现 `IChunkingStrategy` 并注册到 DI 即可
