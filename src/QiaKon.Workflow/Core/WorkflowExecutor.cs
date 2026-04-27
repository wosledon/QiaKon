using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Core;

/// <summary>
/// 流水线执行器实现
/// </summary>
public class WorkflowExecutor : IWorkflowExecutor
{
    private readonly IWorkflowEventBus? _eventBus;

    public WorkflowExecutor(IWorkflowEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> ExecuteAsync(
        IPipeline pipeline,
        WorkflowContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= new WorkflowContext();
        context.PipelineName = pipeline.Name;

        return await pipeline.ExecuteAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PipelineResult> ExecuteAsync(
        IPipeline pipeline,
        IDictionary<string, object> input,
        CancellationToken cancellationToken = default)
    {
        var context = new WorkflowContext { PipelineName = pipeline.Name };

        foreach (var kvp in input)
        {
            context.SetItem(kvp.Key, kvp.Value);
        }

        return await pipeline.ExecuteAsync(context, cancellationToken);
    }
}

/// <summary>
/// 流水线注册表实现
/// </summary>
public class PipelineRegistry : IPipelineRegistry
{
    private readonly Dictionary<string, IPipeline> _pipelines = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(IPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipelines[pipeline.Name] = pipeline;
    }

    /// <inheritdoc />
    public IPipeline? Get(string name)
    {
        return _pipelines.TryGetValue(name, out var pipeline) ? pipeline : null;
    }

    /// <inheritdoc />
    public bool Contains(string name)
    {
        return _pipelines.ContainsKey(name);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllNames()
    {
        return _pipelines.Keys;
    }
}
