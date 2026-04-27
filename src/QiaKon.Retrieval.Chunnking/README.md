# QiaKon.Retrieval.Chunnking

基础分块策略模块，提供基于固定规则的文档分块实现。

## 定位

本模块是 MoE 智能分块的**轻量级替代方案**，不依赖 LLM，纯本地计算，速度极快。

## 分块策略

| 策略 | 类 | 适用场景 |
|------|-----|---------|
| 字符滑动窗口 | `CharacterChunkingStrategy` | 通用文本，追求速度 |
| 段落分块 | `ParagraphChunkingStrategy` | 结构清晰的文档（Markdown、HTML） |

## 快速开始

### 字符分块

```csharp
services.AddCharacterChunking(options =>
{
    options.MaxChunkSize = 2000;   // 每个块最大字符数
    options.OverlapSize = 200;     // 块之间重叠字符数
    options.MinChunkSize = 100;    // 最小块大小
});
```

### 段落分块

```csharp
services.AddParagraphChunking(options =>
{
    options.MaxChunkSize = 2000;
    options.PreserveHeaders = true; // 保留章节标题作为元数据
});
```

## 与 MoE 分块的选择建议

| 场景 | 推荐策略 | 理由 |
|------|---------|------|
| 大规模批量处理 | 字符/段落分块 | 无 LLM 调用成本，速度快 |
| 语义敏感场景 | MoE 分块 | LLM 理解主题边界，分块质量高 |
| 实时处理 | 字符分块 | 毫秒级响应 |
