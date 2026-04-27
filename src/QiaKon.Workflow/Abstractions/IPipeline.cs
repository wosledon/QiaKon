namespace QiaKon.Workflow.Abstractions;

/// <summary>
/// 流水线执行结果
/// </summary>
public class PipelineResult
{
    public bool IsSuccess { get; init; }
    public string PipelineName { get; init; } = string.Empty;
    public IReadOnlyList<StageResult> StageResults { get; init; } = [];
    public TimeSpan TotalDuration { get; init; }
    public IDictionary<string, object?>? Output { get; init; }

    public static PipelineResult Success(string pipelineName, IReadOnlyList<StageResult> results, TimeSpan duration, IDictionary<string, object?>? output = null)
        => new() { IsSuccess = true, PipelineName = pipelineName, StageResults = results, TotalDuration = duration, Output = output };

    public static PipelineResult Failure(string pipelineName, IReadOnlyList<StageResult> results, TimeSpan duration)
        => new() { IsSuccess = false, PipelineName = pipelineName, StageResults = results, TotalDuration = duration };
}

/// <summary>
/// 流水线接口，代表一个完整的执行流水线
/// </summary>
public interface IPipeline
{
    /// <summary>
    /// 流水线名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 流水线描述
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 流水线阶段列表
    /// </summary>
    IReadOnlyList<IStage> Stages { get; }

    /// <summary>
    /// 执行流水线
    /// </summary>
    Task<PipelineResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default);
}
