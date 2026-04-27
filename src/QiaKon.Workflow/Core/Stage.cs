using System.Diagnostics;
using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Core;

/// <summary>
/// 阶段实现
/// </summary>
public class Stage : IStage
{
    private readonly List<IStep> _steps = new();

    public Stage(string name, StepMode mode = StepMode.Sequential)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Mode = mode;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public StepMode Mode { get; }

    /// <inheritdoc />
    public IReadOnlyList<IStep> Steps => _steps.AsReadOnly();

    /// <summary>
    /// 添加步骤
    /// </summary>
    public Stage AddStep(IStep step)
    {
        _steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
        return this;
    }

    /// <summary>
    /// 添加多个步骤
    /// </summary>
    public Stage AddSteps(params IStep[] steps)
    {
        _steps.AddRange(steps);
        return this;
    }

    /// <inheritdoc />
    public async Task<StageResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<StepResult>();

        context.CurrentStageName = Name;

        try
        {
            if (Mode == StepMode.Parallel)
            {
                var tasks = _steps.Select(step => ExecuteStepAsync(step, context, cancellationToken));
                results.AddRange(await Task.WhenAll(tasks));
            }
            else
            {
                foreach (var step in _steps)
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
            }

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

    private async Task<StepResult> ExecuteStepAsync(IStep step, WorkflowContext context, CancellationToken cancellationToken)
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
/// 阶段工厂
/// </summary>
public static class StageFactory
{
    public static Stage Create(string name, StepMode mode = StepMode.Sequential, params IStep[] steps)
    {
        var stage = new Stage(name, mode);
        if (steps.Length > 0)
        {
            stage.AddSteps(steps);
        }
        return stage;
    }
}
