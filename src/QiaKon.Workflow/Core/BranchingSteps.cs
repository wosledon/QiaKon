using QiaKon.Workflow.Abstractions;

namespace QiaKon.Workflow.Core;

/// <summary>
/// 并行步骤，执行多个步骤同时进行
/// </summary>
public class ParallelStep : StepBase
{
    private readonly IReadOnlyList<IStep> _steps;

    public ParallelStep(params IStep[] steps)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public ParallelStep(IEnumerable<IStep> steps)
    {
        _steps = steps.ToList().AsReadOnly();
    }

    public override string Name => $"Parallel[{string.Join(",", _steps.Select(s => s.Name))}]";

    protected override async Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(_steps.Select(s => s.ExecuteAsync(context, cancellationToken)));

        var failedCount = results.Count(r => r.Status == StepResultStatus.Failed);

        return failedCount == 0
            ? StepResult.Succeeded()
            : StepResult.Failed($"{failedCount} of {_steps.Count} steps failed");
    }
}

/// <summary>
/// 条件分支步骤，根据条件选择执行哪个分支
/// </summary>
public class BranchingStep : StepBase
{
    private readonly Func<WorkflowContext, CancellationToken, Task<string>> _branchSelector;
    private readonly Dictionary<string, IStep> _branches;
    private readonly IStep? _defaultBranch;

    public BranchingStep(
        Func<WorkflowContext, CancellationToken, Task<string>> branchSelector,
        Dictionary<string, IStep> branches,
        IStep? defaultBranch = null)
    {
        _branchSelector = branchSelector ?? throw new ArgumentNullException(nameof(branchSelector));
        _branches = branches ?? throw new ArgumentNullException(nameof(branches));
        _defaultBranch = defaultBranch;
    }

    public override string Name => "Branching";

    protected override async Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var branchName = await _branchSelector(context, cancellationToken);

        if (_branches.TryGetValue(branchName, out var step))
        {
            return await step.ExecuteAsync(context, cancellationToken);
        }

        if (_defaultBranch != null)
        {
            return await _defaultBranch.ExecuteAsync(context, cancellationToken);
        }

        return StepResult.Failed($"No branch found for '{branchName}' and no default branch configured");
    }
}

/// <summary>
/// 循环步骤，重复执行直到条件不满足
/// </summary>
public class LoopStep : StepBase
{
    private readonly IStep _innerStep;
    private readonly Func<WorkflowContext, int, CancellationToken, Task<bool>> _continueCondition;
    private readonly int _maxIterations;

    public LoopStep(
        IStep innerStep,
        Func<WorkflowContext, int, CancellationToken, Task<bool>> continueCondition,
        int maxIterations = 100)
    {
        _innerStep = innerStep ?? throw new ArgumentNullException(nameof(innerStep));
        _continueCondition = continueCondition ?? throw new ArgumentNullException(nameof(continueCondition));
        _maxIterations = maxIterations;
    }

    public override string Name => $"Loop({_innerStep.Name})";

    protected override async Task<StepResult> ExecuteCoreAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var iterations = 0;
        var results = new List<StepResult>();

        while (iterations < _maxIterations)
        {
            var shouldContinue = await _continueCondition(context, iterations, cancellationToken);

            if (!shouldContinue)
            {
                break;
            }

            var result = await _innerStep.ExecuteAsync(context, cancellationToken);
            results.Add(result);

            if (result.Status != StepResultStatus.Succeeded)
            {
                return result;
            }

            iterations++;
        }

        return StepResult.Succeeded(new Dictionary<string, object>
        {
            ["Iterations"] = iterations
        });
    }
}
