# QiaKon.Workflow - AGENTS.md

> **模块**: 工作流引擎  
> **职责**: 流程编排（Step/Stage/Pipeline 模式）  
> **依赖**: `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Api`, `QiaKon.Llm.*`

---

## 一、模块职责

本模块提供轻量级流程编排能力，支持复杂业务逻辑的 Step/Stage/Pipeline 组合，与 LLM Agent 深度集成。

**核心职责**:
- 定义 Step（最小执行单元）
- 定义 Stage（阶段，包含多个并行或串行的 Step）
- 定义 Pipeline（完整流程，包含多个 Stage）
- 支持分支、重试、并行执行模式

---

## 二、核心接口

### 2.1 工作流接口

```csharp
public interface IPipeline
{
    string Name { get; }
    Task<PipelineResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

public interface IStep
{
    string Name { get; }
    Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

public interface IStage
{
    string Name { get; }
    StageMode Mode { get; }
    IReadOnlyList<IStep> Steps { get; }
    Task<StageResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

public enum StageMode { Sequential, Parallel }
```

### 2.2 工作流上下文

```csharp
public sealed class WorkflowContext
{
    public string? PipelineName { get; set; }
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
    
    public void SetItem(string key, object value);
    public T? GetItem<T>(string key);
    public bool TryGetItem<T>(string key, out T? value);
}
```

### 2.3 步骤基类

```csharp
public abstract class StepBase : IStep
{
    public required string Name { get; init; }
    protected virtual Task<StepResult> OnExecuteAsync(WorkflowContext context, CancellationToken ct) 
        => Task.FromResult(StepResult.Success());
        
    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default)
    {
        return await OnExecuteAsync(context, ct);
    }
}
```

---

## 三、流程模式

### 3.1 Sequential（串行）

各 Stage 按顺序执行，前一阶段完成才执行下一阶段。

```
Stage 1 → Stage 2 → Stage 3
```

### 3.2 Parallel（并行）

同一 Stage 内的 Step 并行执行。

```
Stage 1: [Step A, Step B, Step C] (并行)
    ↓
Stage 2: [Step D, Step E] (并行)
```

### 3.3 Branching（分支）

根据条件选择执行路径。

```csharp
public class BranchingSteps : StepBase
{
    public required Func<WorkflowContext, string> BranchSelector { get; init; }
    public required IDictionary<string, IStep> Branches { get; init; }
    public IStep? DefaultBranch { get; init; }
}
```

### 3.4 Retry（重试）

```csharp
public class RetryStep : StepBase
{
    public required IStep InnerStep { get; init; }
    public int MaxRetries { get; init; } = 3;
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(1);
    public BackoffType Backoff { get; init; } = BackoffType.Exponential;
}
```

---

## 四、开发规范

### 4.1 创建新 Step

1. 继承 `StepBase` 类
2. 实现 `OnExecuteAsync` 方法
3. 返回 `StepResult.Success()` 或 `StepResult.Failure(error)`
4. 通过 `WorkflowContext` 传递数据

### 4.2 错误处理

- **失败快速**: 默认情况下，Step 失败会中断 Pipeline
- **容错模式**: 设置 `ContinueOnError = true` 继续执行
- **重试机制**: 使用 `RetryStep` 封装需要重试的 Step

### 4.3 数据传递

```csharp
// Step A 设置数据
context.SetItem("userId", userId);

// Step B 获取数据
var userId = context.GetItem<Guid>("userId");
```

### 4.4 类型安全

```csharp
// 使用强类型上下文扩展
public static class WorkflowContextExtensions
{
    public static void SetDocument(this WorkflowContext context, Document doc) 
        => context.SetItem("document", doc);
        
    public static Document? GetDocument(this WorkflowContext context) 
        => context.GetItem<Document>("document");
}
```

---

## 五、与 LLM Agent 集成

### 5.1 Agent 作为 Step

```csharp
public class LlmAgentStep : StepBase
{
    private readonly ILlmAgent _agent;
    
    public LlmAgentStep(ILlmAgent agent) => _agent = agent;
    
    protected override async Task<StepResult> OnExecuteAsync(
        WorkflowContext context, 
        CancellationToken ct)
    {
        var input = context.GetItem<string>("userInput");
        var response = await _agent.ExecuteAsync(new AgentRequest { UserInput = input }, ct);
        context.SetItem("agentResponse", response);
        return StepResult.Success();
    }
}
```

### 5.2 Pipeline 编排示例

```csharp
var pipeline = new PipelineBuilder("RagPipeline")
    .AddStage("Retrieve", StageMode.Sequential, stage => stage
        .AddStep(new QueryUnderstandingStep())
        .AddStep(new VectorSearchStep())
        .AddStep(new KeywordSearchStep())
    )
    .AddStage("Rerank", StageMode.Parallel, stage => stage
        .AddStep(new RelevanceScoringStep())
        .AddStep(new DiversityFilteringStep())
    )
    .AddStage("Generate", StageMode.Sequential, stage => stage
        .AddStep(new PromptBuildingStep())
        .AddStep(new LlmAgentStep(ragAgent))
    )
    .Build();
```

---

## 六、配置示例

```json
{
  "Workflow": {
    "Pipelines": {
      "DocumentProcessing": {
        "Stages": [
          {
            "Name": "Parse",
            "Mode": "Sequential",
            "Steps": ["PdfParseStep", "ContentCleanStep"]
          },
          {
            "Name": "Chunk",
            "Mode": "Sequential",
            "Steps": ["MoEChunkingStep"]
          },
          {
            "Name": "Embed",
            "Mode": "Parallel",
            "Steps": ["EmbeddingStep", "MetadataExtractStep"]
          }
        ]
      }
    }
  }
}
```

---

## 七、测试要求

### 7.1 单元测试

- Step 执行逻辑
- 分支选择逻辑
- 重试机制

### 7.2 集成测试

- Pipeline 完整流程
- 并行执行正确性
- 上下文数据传递

---

## 八、注意事项

1. **线程安全**: 并行 Step 访问共享上下文时需加锁
2. **超时控制**: 长时间运行的 Step 需设置超时
3. **资源释放**: Step 实现 `IDisposable` 管理资源
4. **可观测性**: 记录 Step 执行时间和状态

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
