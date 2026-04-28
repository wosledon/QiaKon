# QiaKon.Retrieval.Chunking.MoE - AGENTS.md

> **模块**: MoE 分块路由  
> **职责**: 根据文档类型自动选择最优分块策略  
> **依赖**: `QiaKon.Retrieval.Chunking`  
> **被依赖**: `QiaKon.Retrieval.DocumentProcessor`

---

## 一、模块职责

本模块实现 Mixture of Experts (MoE) 分块路由，根据文档特征自动选择最合适的分块策略。

**核心职责**:
- `MoEChunkingStrategy` 路由实现
- `MoEChunkingStrategyFactory` 工厂
- 文档类型识别
- 策略注册与管理

---

## 二、核心类

### 2.1 MoEChunkingStrategy

```csharp
public class MoEChunkingStrategy : IChunkingStrategy
{
    private readonly IDictionary<DocumentType, IChunkingStrategy> _experts;
    private readonly IChunkingStrategy _defaultExpert;
    
    public async Task<IReadOnlyList<IChunk>> ChunkAsync(
        IDocument document, 
        ChunkingOptions options, 
        CancellationToken ct = default)
    {
        var expert = GetExpert(document.Type);
        return await expert.ChunkAsync(document, options, ct);
    }
    
    private IChunkingStrategy GetExpert(DocumentType type)
    {
        return _experts.GetValueOrDefault(type, _defaultExpert);
    }
}
```

### 2.2 MoEChunkingStrategyFactory

```csharp
public interface IMoEChunkingStrategyFactory
{
    MoEChunkingStrategy Create(MoEChunkingOptions options);
    void RegisterExpert(DocumentType type, IChunkingStrategy strategy);
}
```

### 2.3 MoEChunkingOptions

```csharp
public class MoEChunkingOptions
{
    public IDictionary<DocumentType, string> ExpertMappings { get; set; } = new();
    public string DefaultExpert { get; set; } = "Recursive";
}
```

---

## 三、文档类型识别

### 3.1 识别规则

| 文档类型  | 特征                      | 推荐策略  |
| --------- | ------------------------- | --------- |
| Markdown  | 包含 `#`, `##` 等标题标记 | Recursive |
| HTML      | 包含 HTML 标签            | Recursive |
| PDF       | 二进制格式，需解析        | Semantic  |
| PlainText | 无结构文本                | Character |
| Table     | 包含表格数据              | Table     |

### 3.2 自动识别

```csharp
public DocumentType DetectType(IDocument document)
{
    if (document.Content.Contains("# ")) return DocumentType.Markdown;
    if (document.Content.Contains("<")) return DocumentType.Html;
    if (document.Content.Contains("|")) return DocumentType.Table;
    return DocumentType.PlainText;
}
```

---

## 四、开发规范

### 4.1 注册专家策略

```csharp
services.AddMoEChunking(options =>
{
    options.RegisterExpert(DocumentType.Markdown, "Recursive");
    options.RegisterExpert(DocumentType.PlainText, "Character");
    options.DefaultExpert = "Recursive";
});
```

### 4.2 添加新专家

1. 实现 `IChunkingStrategy`
2. 在 MoE 选项中注册映射关系
3. 编写测试验证路由逻辑

---

## 五、测试要求

- 文档类型识别准确性
- 路由正确性
- 默认策略回退
- 分块效果对比

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
