using System.Diagnostics;
using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Core;

/// <summary>
/// 步骤基类，提供通用功能
/// </summary>
public abstract class StepBase : IStep
{
    /// <summary>
    /// 步骤名称，默认为类名
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// 执行步骤
    /// </summary>
    public virtual Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                return Task.FromResult(StepResult.Cancelled(stopwatch.Elapsed));
            }

            return ExecuteCoreAsync(context, cancellationToken).WaitAsync(cancellationToken).ContinueWith(t =>
            {
                stopwatch.Stop();
                context.CurrentStepName = Name;

                if (t.IsFaulted && t.Exception != null)
                {
                    return StepResult.Failed(t.Exception.Message, t.Exception.InnerException ?? t.Exception, stopwatch.Elapsed);
                }

                return t.IsCanceled
                    ? StepResult.Cancelled(stopwatch.Elapsed)
                    : t.Result;
            }, TaskScheduler.Default);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Task.FromResult(StepResult.Cancelled(stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(StepResult.Failed(ex.Message, ex, stopwatch.Elapsed));
        }
    }

    /// <summary>
    /// 核心执行逻辑，子类实现
    /// </summary>
    protected abstract Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Lambda 步骤，用于快速创建简单步骤
/// </summary>
public class LambdaStep : StepBase
{
    private readonly Func<WorkflowContext, CancellationToken, Task<StepResult>> _execute;

    public LambdaStep(string name, Func<WorkflowContext, CancellationToken, Task<StepResult>> execute)
    {
        Name = name;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public override string Name { get; }

    protected override Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
        => _execute(context, cancellationToken);
}

/// <summary>
/// 同步 Lambda 步骤
/// </summary>
public class SyncLambdaStep : StepBase
{
    private readonly Action<WorkflowContext, CancellationToken> _execute;
    private readonly Func<WorkflowContext, CancellationToken, StepResult>? _resultFactory;

    public SyncLambdaStep(string name, Action<WorkflowContext, CancellationToken> execute, Func<WorkflowContext, CancellationToken, StepResult>? resultFactory = null)
    {
        Name = name;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _resultFactory = resultFactory;
    }

    public override string Name { get; }

    protected override Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        _execute(context, cancellationToken);
        return Task.FromResult(_resultFactory?.Invoke(context, cancellationToken) ?? StepResult.Succeeded());
    }
}
