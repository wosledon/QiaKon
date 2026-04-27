namespace QiaKon.Workflow.Abstractions;

/// <summary>
/// 工作流事件接口
/// </summary>
public interface IWorkflowEvent
{
    /// <summary>
    /// 事件类型
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    DateTime OccurredAt { get; init; }

    /// <summary>
    /// 关联的流水线名称
    /// </summary>
    string? PipelineName { get; init; }

    /// <summary>
    /// 关联的阶段名称
    /// </summary>
    string? StageName { get; init; }

    /// <summary>
    /// 关联的步骤名称
    /// </summary>
    string? StepName { get; init; }
}

/// <summary>
/// 工作流事件总线接口
/// </summary>
public interface IWorkflowEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IWorkflowEvent;

    /// <summary>
    /// 订阅事件
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IWorkflowEvent;

    /// <summary>
    /// 检查是否有订阅者
    /// </summary>
    bool HasSubscribers<TEvent>() where TEvent : IWorkflowEvent;
}
