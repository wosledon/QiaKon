# QiaKon.Retrieval.Chunking - AGENTS.md

> **模块**: 文档分块  
> **职责**: 分块策略抽象、内置分块实现  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Retrieval`  
> **被依赖**: `QiaKon.Retrieval.Chunking.MoE`, `QiaKon.Retrieval.DocumentProcessor`

---

## 一、模块职责

本模块提供文档分块的策略抽象和内置实现，支持多种分块方式。

**核心职责**:
- `IChunkingStrategy` 接口定义
- 内置分块策略（字符、段落、递归）
- 分块配置与选项
- 分块结果验证

---

## 二、核心接口

### 2.1 IChunkingStrategy

```csharp
public interface IChunkingStrategy
{
    string Name { get; }
    Task<IReadOnlyList<IChunk>> ChunkAsync(
        IDocument document, 
        ChunkingOptions options, 
        CancellationToken ct = default);
}
```

### 2.2 IChunk

```csharp
public interface IChunk
{
    Guid Id { get; }
    Guid DocumentId { get; }
    string Content { get; }
    int Order { get; }
    IReadOnlyDictionary<string, object>? Metadata { get; }
}
```

---

## 三、内置分块策略

### 3.1 CharacterChunkingStrategy

按固定字符数切分：

```csharp
var options = new ChunkingOptions
{
    ChunkSize = 512,
    Overlap = 50
};
```

**适用场景**: 简单文本、无结构文档

### 3.2 ParagraphChunkingStrategy

按段落切分，保持段落完整性：

**适用场景**: 文章、报告等结构化文档

### 3.3 RecursiveChunkingStrategy

按层级递归切分（标题 → 段落 → 句子）：

```csharp
var options = new ChunkingOptions
{
    Separators = new[] { "\n## ", "\n### ", "\n", " " },
    ChunkSize = 1000,
    Overlap = 100
};
```

**适用场景**: Markdown、HTML 等有层级结构的文档

---

## 四、开发规范

### 4.1 添加新分块策略

1. 实现 `IChunkingStrategy` 接口
2. 命名规范：`{StrategyName}ChunkingStrategy`
3. 编写单元测试验证分块边界
4. 更新 MoE 路由注册

### 4.2 分块质量验证

- 检查分块大小是否在合理范围
- 检查重叠度是否符合配置
- 检查元数据是否正确传递

---

## 五、测试要求

- 分块边界正确性
- 重叠度验证
- 大文档分块性能
- 空文档/边界情况处理

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
