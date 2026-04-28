# QiaKon.Llm.Tokenization - AGENTS.md

> **模块**: Token 计算  
> **职责**: Token 计数、Tokenizer 管理、多模型支持  
> **依赖**: `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Llm.Context`, `QiaKon.Llm.Providers`

---

## 一、模块职责

本模块提供 LLM Token 计数能力，支持多种 Tokenizer，用于上下文长度估算和成本控制。

**核心职责**:
- `ITokenizer` 接口定义
- 多模型 Tokenizer 实现
- `TokenizerService` 统一管理
- Token 计数与估算

---

## 二、核心接口

### 2.1 ITokenizer

```csharp
public interface ITokenizer
{
    string ModelName { get; }
    int CountTokens(string text);
    int CountTokens(IEnumerable<string> texts);
    string? Decode(int[] tokenIds);
    int[] Encode(string text);
}
```

### 2.2 TokenizerService

```csharp
public interface ITokenizerService
{
    ITokenizer GetTokenizer(string modelName);
    void RegisterTokenizer(string modelName, ITokenizer tokenizer);
}
```

---

## 三、Tokenizer 实现

### 3.1 OpenAI Tokenizer

使用 `tiktoken` 库，支持：
- `cl100k_base` (GPT-4/3.5)
- `p50k_base` (GPT-3)
- `r50k_base` (GPT-2)

### 3.2 本地 Tokenizer

使用 HuggingFace Tokenizer，支持开源模型。

---

## 四、开发规范

### 4.1 使用示例

```csharp
var tokenizer = tokenizerService.GetTokenizer("gpt-4");
var tokenCount = tokenizer.CountTokens("你好，世界！");
```

### 4.2 注册 Tokenizer

```csharp
services.AddTokenizers(options =>
{
    options.RegisterOpenAITokenizer("gpt-4", "cl100k_base");
    options.RegisterOpenAITokenizer("gpt-3.5-turbo", "cl100k_base");
});
```

---

## 五、测试要求

- Token 计数准确性
- 多模型支持
- 编码/解码正确性

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
