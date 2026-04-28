using System.Diagnostics;
using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Core;

/// <summary>
/// 阶段基类，提供通用功能
/// </summary>
public abstract class StageBase : IStage
{
    private readonly List<IStep> _steps = new();

    /// <summary>
    /// 阶段名称，默认为类名
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public abstract StepMode Mode { get; }

    /// <inheritdoc />
    public IReadOnlyList<IStep> Steps => _steps.AsReadOnly();

    /// <summary>
    /// 添加步骤
    /// </summary>
    protected StageBase AddStep(IStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Add(step);
        return this;
    }

    /// <inheritdoc />
    public virtual async Task<StageResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<StepResult>();

        context.CurrentStageName = Name;

        try
        {
            results.AddRange(await ExecuteCoreAsync(context, cancellationToken));
            stopwatch.Stop();

            var isSuccess = results.All(r => r.Status == StepResultStatus.Succeeded);
            return isSuccess
                ? StageResult.Success(Name, results, stopwatch.Elapsed)
                : StageResult.Failure(Name, results, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            results.Add(StepResult.Cancelled(stopwatch.Elapsed));
            return StageResult.Failure(Name, results, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 核心执行逻辑
    /// </summary>
    protected abstract Task<IReadOnlyList<StepResult>> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken);

    /// <summary>
    /// 执行单个步骤
    /// </summary>
    protected async Task<StepResult> ExecuteStepAsync(IStep step, WorkflowContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await step.ExecuteAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            return StepResult.Failed(ex.Message, ex);
        }
    }
}

/// <summary>
/// 串行阶段基类
/// </summary>
public abstract class SequentialStageBase : StageBase
{
    /// <inheritdoc />
    public override StepMode Mode => StepMode.Sequential;

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<StepResult>> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var results = new List<StepResult>();

        foreach (var step in Steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(StepResult.Cancelled());
                break;
            }

            var result = await ExecuteStepAsync(step, context, cancellationToken);
            results.Add(result);

            if (result.Status == StepResultStatus.Failed)
            {
                break;
            }
        }

        return results;
    }
}

/// <summary>
/// 并行阶段基类
/// </summary>
public abstract class ParallelStageBase : StageBase
{
    /// <inheritdoc />
    public override StepMode Mode => StepMode.Parallel;

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<StepResult>> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var tasks = Steps.Select(step => ExecuteStepAsync(step, context, cancellationToken));
        return await Task.WhenAll(tasks);
    }
}
