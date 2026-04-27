using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Events;

/// <summary>
/// 工作流步骤开始事件
/// </summary>
public sealed class StepStartedEvent : IWorkflowEvent
{
    public string EventType => "StepStarted";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? PipelineName { get; init; }
    public string? StageName { get; init; }
    public string? StepName { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 工作流步骤完成事件
/// </summary>
public sealed class StepCompletedEvent : IWorkflowEvent
{
    public string EventType => "StepCompleted";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? PipelineName { get; init; }
    public string? StageName { get; init; }
    public string? StepName { get; init; }
    public string? CorrelationId { get; init; }
    public StepResultStatus Status { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// 工作流步骤失败事件
/// </summary>
public sealed class StepFailedEvent : IWorkflowEvent
{
    public string EventType => "StepFailed";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? PipelineName { get; init; }
    public string? StageName { get; init; }
    public string? StepName { get; init; }
    public string? CorrelationId { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
}

/// <summary>
/// 工作流阶段开始事件
/// </summary>
public sealed class StageStartedEvent : IWorkflowEvent
{
    public string EventType => "StageStarted";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? PipelineName { get; init; }
    public string? StageName { get; init; }
    public string? StepName { get; init; } = null;
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 工作流阶段完成事件
/// </summary>
public sealed class StageCompletedEvent : IWorkflowEvent
{
    public string EventType => "StageCompleted";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? PipelineName { get; init; }
    public string? StageName { get; init; }
    public string? StepName { get; init; } = null;
    public string? CorrelationId { get; init; }
    public bool IsSuccess { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// 工作流开始事件
/// </summary>
public sealed class WorkflowStartedEvent : IWorkflowEvent
{
    public string EventType => "WorkflowStarted";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? PipelineName { get; init; }
    public string? StageName { get; init; } = null;
    public string? StepName { get; init; } = null;
    public string? CorrelationId { get; init; }
    public IDictionary<string, object>? Input { get; init; }
}

/// <summary>
/// 工作流完成事件
/// </summary>
public sealed class WorkflowCompletedEvent : IWorkflowEvent
{
    public string EventType => "WorkflowCompleted";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? PipelineName { get; init; }
    public string? StageName { get; init; } = null;
    public string? StepName { get; init; } = null;
    public string? CorrelationId { get; init; }
    public bool IsSuccess { get; init; }
    public TimeSpan Duration { get; init; }
    public IDictionary<string, object?>? Output { get; init; }
}
