using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Abstractions;

/// <summary>
/// 阶段执行结果
/// </summary>
public class StageResult
{
    public bool IsSuccess { get; init; }
    public string StageName { get; init; } = string.Empty;
    public IReadOnlyList<StepResult> StepResults { get; init; } = [];
    public TimeSpan TotalDuration { get; init; }

    public static StageResult Success(string stageName, IReadOnlyList<StepResult> results, TimeSpan duration)
        => new() { IsSuccess = true, StageName = stageName, StepResults = results, TotalDuration = duration };

    public static StageResult Failure(string stageName, IReadOnlyList<StepResult> results, TimeSpan duration)
        => new() { IsSuccess = false, StageName = stageName, StepResults = results, TotalDuration = duration };
}

/// <summary>
/// 阶段接口，代表流水线中的一个执行阶段
/// </summary>
public interface IStage
{
    /// <summary>
    /// 阶段名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 阶段执行模式
    /// </summary>
    StepMode Mode { get; }

    /// <summary>
    /// 阶段内的步骤
    /// </summary>
    IReadOnlyList<IStep> Steps { get; }

    /// <summary>
    /// 执行阶段
    /// </summary>
    Task<StageResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default);
}
