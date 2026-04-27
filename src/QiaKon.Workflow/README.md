# QiaKon.Workflow

流水线编排框架，支持 **Agent 编排**和**工作流编排**，配置驱动与事件驱动双模式。

## 核心概念

| 概念         | 说明                                   |
| ------------ | -------------------------------------- |
| **Step**     | 最小执行单元，对应一个具体操作         |
| **Stage**    | 阶段，包含一组 Step，支持串行/并行执行 |
| **Pipeline** | 流水线，顶层编排容器，包含多个 Stage   |
| **Context**  | 执行上下文，贯穿整个流水线生命周期     |
| **Event**    | 事件，驱动监控、调试、外部集成         |

## 安装

```bash
dotnet add package QiaKon.Workflow
```

## 快速开始

### 1. 定义步骤

```csharp
using QiaKon.Workflow.Abstractions;
using QiaKon.Workflow.Core;

// Lambda 步骤（最快捷）
var fetchStep = new LambdaStep("FetchData", async (ctx, ct) =>
{
    var data = await HttpClient.GetStringAsync("https://api.example.com/data", ct);
    ctx.SetItem("data", data);
    return StepResult.Succeeded();
});

// 继承 StepBase（复杂逻辑）
public class TransformStep : StepBase
{
    protected override async Task<StepResult> ExecuteCoreAsync(WorkflowContext ctx, CancellationToken ct)
    {
        var data = ctx.GetItem<string>("data");
        var transformed = Transform(data);
        ctx.SetItem("result", transformed);
        return StepResult.Succeeded();
    }
}
```

### 2. 构建流水线

```csharp
// 方式一：工厂方法
var pipeline = PipelineFactory.Create("MyPipeline", "描述文本")
    .AddStage(
        StageFactory.Create("准备阶段", StepMode.Sequential,
            new LambdaStep("Step1", async (ctx, ct) => StepResult.Succeeded()),
            new LambdaStep("Step2", async (ctx, ct) => StepResult.Succeeded())
        )
    )
    .AddStage(new Stage("执行阶段", StepMode.Parallel)
        .AddStep(new LambdaStep("Parallel1", (ctx, ct) => Task.FromResult(StepResult.Succeeded())))
        .AddStep(new LambdaStep("Parallel2", (ctx, ct) => Task.FromResult(StepResult.Succeeded())))
    );

// 方式二： fluent API
var pipeline = new Pipeline("MyPipeline")
    .AddStage(new Stage("Stage1").AddSteps(step1, step2))
    .AddStage(new Stage("Stage2", StepMode.Parallel).AddSteps(step3, step4, step5));
```

### 3. 执行流水线

```csharp
var executor = new WorkflowExecutor();
var context = new WorkflowContext();

// 设置初始输入
context.SetItem("requestId", "12345");

// 执行
var result = await executor.ExecuteAsync(pipeline, context);

if (result.IsSuccess)
{
    var output = result.Output;
    Console.WriteLine($"完成，耗时 {result.TotalDuration}");
}
else
{
    Console.WriteLine($"失败: {result.StageResults.LastOrDefault()?.StepResults.LastOrDefault()?.ErrorMessage}");
}
```

## 高级特性

### 步骤包装器

```csharp
// 重试包装器（指数退避）
var retryStep = new RetryStepWrapper(failableStep, maxRetries: 3);

// 超时包装器
var timeoutStep = new TimeoutStepWrapper(step, TimeSpan.FromSeconds(30));

// 条件执行包装器
var conditionalStep = new ConditionalStepWrapper(
    step,
    async (ctx, ct) => ctx.GetItem<bool>("enableFeature")
);
```

### 高级步骤类型

```csharp
// 并行执行步骤
var parallelStep = new ParallelStep(step1, step2, step3);

// 条件分支步骤
var branchStep = new BranchingStep(
    async (ctx, ct) => ctx.GetItem<string>("branch"),
    new Dictionary<string, IStep>
    {
        ["A"] = stepA,
        ["B"] = stepB,
    },
    defaultBranch: fallbackStep
);

// 循环步骤
var loopStep = new LoopStep(
    innerStep,
    (ctx, iteration, ct) => iteration < 10 && !ctx.IsCancellationRequested
);
```

### 事件驱动

```csharp
var eventBus = new WorkflowEventBus();

// 订阅事件
eventBus.Subscribe<StepCompletedEvent>(async e =>
{
    Console.WriteLine($"[StepCompleted] {e.StepName} | {e.Duration}");
});

eventBus.Subscribe<WorkflowCompletedEvent>(async e =>
{
    await MetricsClient.TrackDurationAsync(e.PipelineName, e.Duration);
});

// 带事件总线的执行器
var executor = new WorkflowExecutor(eventBus);
await executor.ExecuteAsync(pipeline, context);
```

### 配置驱动

```csharp
// JSON 配置
var json = """
{
  "name": "DataPipeline",
  "stages": [
    {
      "name": "Extract",
      "mode": "Sequential",
      "steps": [
        { "name": "FetchFromApi", "type": "HttpFetch" },
        { "name": "ParseResponse", "type": "JsonParse" }
      ]
    },
    {
      "name": "Transform",
      "mode": "Parallel",
      "steps": [
        { "name": "CleanData", "type": "DataClean" },
        { "name": "EnrichData", "type": "DataEnrich" }
      ]
    }
  ]
}
""";

// 加载配置
var config = PipelineConfigurationLoader.LoadFromJson(json);
var pipeline = PipelineConfigurationLoader.BuildPipeline(config, stepFactory);
```

### 依赖注入集成

```csharp
// Program.cs
builder.Services.AddWorkflowCore();
builder.Services.RegisterPipeline(myPipeline);

// 在服务中使用
public class MyService
{
    private readonly IWorkflowExecutor _executor;
    private readonly IPipelineRegistry _registry;

    public MyService(IWorkflowExecutor executor, IPipelineRegistry registry)
    {
        _executor = executor;
        _registry = registry;
    }

    public async Task RunAsync()
    {
        var pipeline = _registry.Get("MyPipeline");
        var result = await _executor.ExecuteAsync(pipeline!);
    }
}
```

## 事件类型

| 事件                     | 说明       |
| ------------------------ | ---------- |
| `WorkflowStartedEvent`   | 流水线开始 |
| `WorkflowCompletedEvent` | 流水线完成 |
| `StageStartedEvent`      | 阶段开始   |
| `StageCompletedEvent`    | 阶段完成   |
| `StepStartedEvent`       | 步骤开始   |
| `StepCompletedEvent`     | 步骤完成   |
| `StepFailedEvent`        | 步骤失败   |

## 设计原则

1. **最小惊讶原则** - API 设计直观，行为符合预期
2. **组合优于继承** - 通过包装器扩展功能，而非继承
3. **关注点分离** - Step/Stage/Pipeline 各司其职
4. **不可变性** - 执行结果不可变，便于追踪和调试
5. **可观测性** - 内置事件机制，支持全链路监控
