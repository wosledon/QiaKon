namespace QiaKon.Workflow.Abstractions;

/// <summary>
/// 流水线执行器接口
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// 执行流水线
    /// </summary>
    Task<PipelineResult> ExecuteAsync(
        IPipeline pipeline,
        WorkflowContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行流水线（带初始输入）
    /// </summary>
    Task<PipelineResult> ExecuteAsync(
        IPipeline pipeline,
        IDictionary<string, object> input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 流水线注册表接口，用于管理多个流水线
/// </summary>
public interface IPipelineRegistry
{
    /// <summary>
    /// 注册流水线
    /// </summary>
    void Register(IPipeline pipeline);

    /// <summary>
    /// 获取流水线
    /// </summary>
    IPipeline? Get(string name);

    /// <summary>
    /// 检查流水线是否存在
    /// </summary>
    bool Contains(string name);

    /// <summary>
    /// 获取所有流水线名称
    /// </summary>
    IEnumerable<string> GetAllNames();
}
