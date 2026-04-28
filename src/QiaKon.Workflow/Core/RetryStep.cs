using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Core;

/// <summary>
/// 重试步骤包装器
/// </summary>
public class RetryStepWrapper : StepBase
{
    private readonly IStep _innerStep;
    private readonly int _maxRetries;
    private readonly Func<int, TimeSpan>? _delayFactory;

    public RetryStepWrapper(IStep innerStep, int maxRetries, Func<int, TimeSpan>? delayFactory = null)
    {
        _innerStep = innerStep ?? throw new ArgumentNullException(nameof(innerStep));
        _maxRetries = maxRetries;
        _delayFactory = delayFactory ?? (retryCount => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount)));
    }

    public override string Name => $"Retry({_innerStep.Name})";

    protected override async Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var result = await _innerStep.ExecuteAsync(context, cancellationToken);

                if (result.Status == StepResultStatus.Succeeded)
                {
                    return result;
                }

                if (result.Status == StepResultStatus.Failed)
                {
                    lastException = result.Exception ?? new InvalidOperationException("Step failed");
                }
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                lastException = ex;
            }

            if (attempt < _maxRetries)
            {
                var delay = _delayFactory!(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }

        return StepResult.Failed(
            $"Step failed after {_maxRetries + 1} attempts",
            lastException ?? new InvalidOperationException("Step failed after retries"));
    }
}

/// <summary>
/// 超时步骤包装器
/// </summary>
public class TimeoutStepWrapper : StepBase
{
    private readonly IStep _innerStep;
    private readonly TimeSpan _timeout;

    public TimeoutStepWrapper(IStep innerStep, TimeSpan timeout)
    {
        _innerStep = innerStep ?? throw new ArgumentNullException(nameof(innerStep));
        _timeout = timeout;
    }

    public override string Name => $"Timeout({_innerStep.Name})";

    protected override async Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await _innerStep.ExecuteAsync(context, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return StepResult.Failed($"Step timed out after {_timeout}");
        }
    }
}

/// <summary>
/// 条件步骤包装器
/// </summary>
public class ConditionalStepWrapper : StepBase
{
    private readonly IStep _innerStep;
    private readonly Func<WorkflowContext, CancellationToken, Task<bool>> _condition;

    public ConditionalStepWrapper(IStep innerStep, Func<WorkflowContext, CancellationToken, Task<bool>> condition)
    {
        _innerStep = innerStep ?? throw new ArgumentNullException(nameof(innerStep));
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public override string Name => $"Conditional({_innerStep.Name})";

    protected override async Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var shouldExecute = await _condition(context, cancellationToken);

        if (!shouldExecute)
        {
            return StepResult.Skipped();
        }

        return await _innerStep.ExecuteAsync(context, cancellationToken);
    }
}
