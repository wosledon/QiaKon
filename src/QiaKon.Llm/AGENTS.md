# QiaKon.Llm - AGENTS.md

> **模块**: LLM 核心引擎  
> **职责**: LLM 调用封装、Agent 编排、Prompt 管理、上下文工程  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Workflow`  
> **被依赖**: `QiaKon.Api`, `QiaKon.Retrieval.*`, `QiaKon.Graph.Engine.*`

---

## 一、模块职责

本模块提供基于 LLM 的智能代理能力，支持多轮对话、工具调用、复杂任务分解与执行。

**核心职责**:
- LLM Provider 抽象与多厂商支持
- 对话上下文管理（`ConversationContext`）
- Prompt 模板管理（`PromptTemplate`）
- Agent 编排（`LlmAgent`, `AgentChain`, `AgentPool`）
- Token 计算与上下文裁剪

---

## 二、子模块架构

### 2.1 子模块总览

| 子模块                    | 职责                              | 文档                                              |
| ------------------------- | --------------------------------- | ------------------------------------------------- |
| `QiaKon.Llm.Context`      | 对话上下文管理、消息裁剪、模板    | [AGENTS.md](../QiaKon.Llm.Context/AGENTS.md)      |
| `QiaKon.Llm.Prompt`       | Prompt 模板管理、变量替换         | [AGENTS.md](../QiaKon.Llm.Prompt/AGENTS.md)       |
| `QiaKon.Llm.Providers`    | LLM 厂商适配（OpenAI/Azure/本地） | [AGENTS.md](../QiaKon.Llm.Providers/AGENTS.md)    |
| `QiaKon.Llm.Tokenization` | Token 计算、上下文估算            | [AGENTS.md](../QiaKon.Llm.Tokenization/AGENTS.md) |

### 2.2 依赖关系

```
QiaKon.Llm
├── QiaKon.Llm.Context (上下文管理)
├── QiaKon.Llm.Prompt (Prompt 模板)
├── QiaKon.Llm.Providers (LLM 厂商适配)
└── QiaKon.Llm.Tokenization (Token 计算)
```

---

## 三、核心接口

### 3.1 LLM 客户端接口

```csharp
public interface ILlmClient
{
    string Model { get; }
    
    Task<ChatCompletion> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);
        
    IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);
}
```

### 3.2 Agent 接口

```csharp
public interface ILlmAgent
{
    string Name { get; }
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct = default);
}
```

### 3.3 工具定义

```csharp
public sealed class LlmTool
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ParametersJsonSchema { get; init; }
}
```

---

## 四、上下文工程

### 4.1 对话上下文

```csharp
public sealed class ConversationContext
{
    public void AddMessage(ChatMessage message);
    public IReadOnlyList<ChatMessage> GetMessages();
    public int EstimateTokenCount(Func<string, int>? tokenCounter = null);
    public void SetSystemPrompt(string systemPrompt);
}
```

### 4.2 消息裁剪策略

| 策略              | 说明              | 适用场景 |
| ----------------- | ----------------- | -------- |
| `DefaultTrimmer`  | 保留最近 N 条消息 | 普通对话 |
| `PriorityTrimmer` | 保留高优先级消息  | 关键对话 |
| `SummaryTrimmer`  | 旧消息压缩为摘要  | 长对话   |

### 4.3 上下文模板

```yaml
name: "qa-assistant"
systemPromptTemplate: "你是专业的问答助手，专注于回答 {{domain}} 相关问题。"
maxMessages: 20
maxTokens: 8000
```

---

## 五、Agent 编排

### 5.1 内置 Agent 类型

| Agent           | 说明               | 适用场景     |
| --------------- | ------------------ | ------------ |
| `RagAgent`      | 文档检索与问答     | RAG 问答     |
| `GraphAgent`    | 知识图谱查询与推理 | 图谱问答     |
| `ApiAgent`      | 调用外部 API       | 业务操作     |
| `WorkflowAgent` | 调用工作流         | 复杂业务流程 |

### 5.2 编排模式

| 模式              | 说明              | 适用场景 |
| ----------------- | ----------------- | -------- |
| `SingleAgent`     | 单 Agent 独立执行 | 简单问答 |
| `SequentialChain` | 串联执行          | 多步推理 |
| `ParallelPool`    | 并行执行          | 多源检索 |
| `RouterAgent`     | 路由分发          | 意图分流 |

### 5.3 工具调用流程

```
用户输入 → LLM 生成 → 需要工具？
  ↓ 是
执行工具 → 工具结果 → LLM 继续生成
  ↓ 否
返回最终回复
```

---

## 六、LLM Provider 适配

### 6.1 已支持厂商

| 厂商         | 模块                   | 说明           |
| ------------ | ---------------------- | -------------- |
| OpenAI       | `OpenAiLlmClient`      | GPT-4/3.5 系列 |
| Azure OpenAI | `AzureOpenAiLlmClient` | Azure 部署     |
| 本地模型     | `LocalLlmClient`       | Ollama/vLLM    |

### 6.2 添加新 Provider 流程

1. 实现 `ILlmClient` 接口
2. 创建配置类 `{Provider}LlmOptions`
3. 实现重试策略和错误处理
4. 编写 `ServiceCollectionExtensions` 注册扩展
5. 支持流式输出和工具调用
6. 编写单元测试

---

## 七、开发规范

### 7.1 Prompt 设计原则

1. **明确角色**: 系统提示词清晰定义 Agent 角色
2. **结构化输入**: 使用模板变量，避免硬编码
3. **约束输出**: 指定输出格式（JSON/Markdown）
4. **Few-Shot**: 提供示例提升准确性

### 7.2 错误处理

- **速率限制**: 实现指数退避重试
- **Token 超限**: 上下文裁剪后重试
- **模型错误**: 记录日志，返回友好错误信息

### 7.3 流式输出

```csharp
await foreach (var chunk in client.CompleteStreamAsync(request))
{
    await response.WriteAsync(chunk.Content);
    await response.Body.FlushAsync();
}
```

---

## 八、配置示例

```json
{
  "Llm": {
    "DefaultProvider": "OpenAI",
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-...",
        "Model": "gpt-4",
        "Temperature": 0.7,
        "MaxTokens": 2000,
        "RetryPolicy": {
          "MaxRetries": 3,
          "BaseDelay": "00:00:01",
          "Backoff": "Exponential"
        }
      }
    }
  }
}
```

---

## 九、测试要求

### 9.1 单元测试

- 上下文管理逻辑
- 消息裁剪策略
- Prompt 模板渲染
- Token 计算准确性

### 9.2 集成测试

- LLM Provider 调用（使用 Mock）
- Agent 编排流程
- 工具调用逻辑

---

## 十、注意事项

1. **API 密钥安全**: 使用 KeyVault 或环境变量，不硬编码
2. **成本控制**: 记录 Token 使用量，设置预算告警
3. **版本兼容**: 不同模型版本的 API 差异需适配
4. **日志记录**: 记录 Prompt 和响应（脱敏），便于调试

---

**最后更新**: 2026-04-28  
**维护者**: AI 工程师 Agent
