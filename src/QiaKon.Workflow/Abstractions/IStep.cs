namespace QiaKon.Workflow.Abstractions;

/// <summary>
/// 流水线步骤执行模式
/// </summary>
public enum StepMode
{
    /// <summary>
    /// 串行执行
    /// </summary>
    Sequential,

    /// <summary>
    /// 并行执行
    /// </summary>
    Parallel
}

/// <summary>
/// 步骤执行结果
/// </summary>
public enum StepResultStatus
{
    Succeeded,
    Failed,
    Skipped,
    Cancelled
}

/// <summary>
/// 步骤执行结果
/// </summary>
public class StepResult
{
    public StepResultStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public IDictionary<string, object>? Output { get; init; }
    public TimeSpan Duration { get; init; }

    public static StepResult Succeeded(IDictionary<string, object>? output = null, TimeSpan? duration = null)
        => new() { Status = StepResultStatus.Succeeded, Output = output, Duration = duration ?? TimeSpan.Zero };

    public static StepResult Failed(string message, Exception? exception = null, TimeSpan? duration = null)
        => new() { Status = StepResultStatus.Failed, ErrorMessage = message, Exception = exception, Duration = duration ?? TimeSpan.Zero };

    public static StepResult Skipped(TimeSpan? duration = null)
        => new() { Status = StepResultStatus.Skipped, Duration = duration ?? TimeSpan.Zero };

    public static StepResult Cancelled(TimeSpan? duration = null)
        => new() { Status = StepResultStatus.Cancelled, Duration = duration ?? TimeSpan.Zero };
}

/// <summary>
/// 步骤接口，所有步骤实现必须实现此接口
/// </summary>
public interface IStep
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行步骤
    /// </summary>
    Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 步骤过滤器接口，用于条件判断
/// </summary>
public interface IStepFilter
{
    /// <summary>
    /// 判断步骤是否应该执行
    /// </summary>
    ValueTask<bool> ShouldExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认步骤过滤器，始终返回 true
/// </summary>
public class DefaultStepFilter : IStepFilter
{
    public ValueTask<bool> ShouldExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(true);
}
