using System.Diagnostics;
using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Core;

/// <summary>
/// 流水线实现
/// </summary>
public class Pipeline : IPipeline
{
    private readonly List<IStage> _stages = new();

    public Pipeline(string name, string? description = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string? Description { get; }

    /// <inheritdoc />
    public IReadOnlyList<IStage> Stages => _stages.AsReadOnly();

    /// <summary>
    /// 添加阶段
    /// </summary>
    public Pipeline AddStage(IStage stage)
    {
        _stages.Add(stage ?? throw new ArgumentNullException(nameof(stage)));
        return this;
    }

    /// <summary>
    /// 添加多个阶段
    /// </summary>
    public Pipeline AddStages(params IStage[] stages)
    {
        _stages.AddRange(stages);
        return this;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<StageResult>();

        context.PipelineName = Name;

        try
        {
            foreach (var stage in _stages)
            {
                if (cancellationToken.IsCancellationRequested || context.IsCancellationRequested)
                {
                    break;
                }

                var stageResult = await stage.ExecuteAsync(context, cancellationToken);
                results.Add(stageResult);

                if (!stageResult.IsSuccess)
                {
                    break;
                }
            }

            stopwatch.Stop();

            var isSuccess = results.All(r => r.IsSuccess);
            var outputs = context.GetOutputs().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return isSuccess
                ? PipelineResult.Success(Name, results, stopwatch.Elapsed, outputs)
                : PipelineResult.Failure(Name, results, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return PipelineResult.Failure(Name, results, stopwatch.Elapsed);
        }
    }
}

/// <summary>
/// 流水线工厂
/// </summary>
public static class PipelineFactory
{
    public static Pipeline Create(string name, string? description = null, params IStage[] stages)
    {
        var pipeline = new Pipeline(name, description);
        if (stages.Length > 0)
        {
            pipeline.AddStages(stages);
        }
        return pipeline;
    }
}
