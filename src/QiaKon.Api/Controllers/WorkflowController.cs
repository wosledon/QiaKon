using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts;
using QiaKon.Workflow.Abstractions;
using StageResultList = System.Collections.Generic.IReadOnlyList<QiaKon.Workflow.Abstractions.StageResult>;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowExecutor _workflowExecutor;
    private readonly IPipelineRegistry _pipelineRegistry;
    private readonly ILogger<WorkflowController> _logger;

    private static readonly Dictionary<Guid, WorkflowExecution> _executions = new();
    private static readonly List<PipelineDefinition> _pipelineDefinitions = new();

    public WorkflowController(
        IWorkflowExecutor workflowExecutor,
        IPipelineRegistry pipelineRegistry,
        ILogger<WorkflowController> logger)
    {
        _workflowExecutor = workflowExecutor;
        _pipelineRegistry = pipelineRegistry;
        _logger = logger;
    }

    private (IPipeline? Pipeline, PipelineDefinition? Definition, string PipelineName) ResolveWorkflow(string id)
    {
        var pipeline = _pipelineRegistry.Get(id);
        if (pipeline is not null)
        {
            return (pipeline, null, pipeline.Name);
        }

        var definition = _pipelineDefinitions.FirstOrDefault(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Name, id, StringComparison.OrdinalIgnoreCase));

        return definition is null
            ? (null, null, string.Empty)
            : (null, definition, definition.Name);
    }

    /// <summary>
    /// 工作流列表
    /// </summary>
    [HttpGet]
    public ApiResponse<IEnumerable<PipelineDefinition>> GetPipelines()
    {
        var pipelineNames = _pipelineRegistry.GetAllNames();
        var definitions = pipelineNames.Select(name =>
        {
            var pipeline = _pipelineRegistry.Get(name);
            return new PipelineDefinition
            {
                Id = name,
                Name = name,
                Description = pipeline?.Description ?? string.Empty,
                StageCount = pipeline?.Stages.Count ?? 0,
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            };
        }).ToList();

        definitions.AddRange(_pipelineDefinitions);

        return ApiResponse<IEnumerable<PipelineDefinition>>.Ok(definitions);
    }

    /// <summary>
    /// 创建工作流定义
    /// </summary>
    [HttpPost]
    public ApiResponse<PipelineDefinition> CreatePipeline([FromBody] CreatePipelineRequest request)
    {
        try
        {
            var definition = new PipelineDefinition
            {
                Id = $"wf_{Guid.NewGuid():N}",
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                Config = request.Config ?? new Dictionary<string, object?>(),
                CreatedAt = DateTime.UtcNow
            };

            _pipelineDefinitions.Add(definition);
            return ApiResponse<PipelineDefinition>.Ok(definition, "工作流创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建工作流失败");
            return ApiResponse<PipelineDefinition>.Fail("创建工作流失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 执行工作流
    /// </summary>
    [HttpPost("{id}/execute")]
    public async Task<ApiResponse<ExecutionResult>> ExecuteWorkflow(
        string id,
        [FromBody] ExecuteWorkflowRequest? request)
    {
        try
        {
            var (pipeline, definition, pipelineName) = ResolveWorkflow(id);
            if (pipeline == null)
            {
                if (definition == null)
                    return ApiResponse<ExecutionResult>.Fail("工作流不存在", 404);

                // 使用 mock 执行
                var executionId = Guid.NewGuid();
                var mockExecution = new WorkflowExecution
                {
                    Id = executionId,
                    PipelineName = pipelineName,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    Input = request?.Input
                };
                _executions[executionId] = mockExecution;

                // 模拟异步执行
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    mockExecution.Status = "Completed";
                    mockExecution.CompletedAt = DateTime.UtcNow;
                    mockExecution.Result = PipelineResult.Success(
                        pipelineName,
                        new List<StageResult>(),
                        TimeSpan.FromMilliseconds(100),
                        new Dictionary<string, object?> { ["output"] = $"Workflow {pipelineName} executed successfully" });
                });

                return ApiResponse<ExecutionResult>.Ok(new ExecutionResult
                {
                    ExecutionId = executionId,
                    PipelineName = pipelineName,
                    Status = "Running"
                }, "工作流已开始执行");
            }

            var context = new WorkflowContext { PipelineName = pipeline.Name };
            if (request?.Input != null)
            {
                foreach (var kvp in request.Input)
                {
                    context.SetItem(kvp.Key, kvp.Value);
                }
            }

            var executionGuid = Guid.NewGuid();
            var execution = new WorkflowExecution
            {
                Id = executionGuid,
                PipelineName = pipeline.Name,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                Input = request?.Input
            };
            _executions[executionGuid] = execution;

            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _workflowExecutor.ExecuteAsync(pipeline, context);
                    execution.Status = result.IsSuccess ? "Completed" : "Failed";
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.Result = result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "工作流执行失败: {ExecutionId}", executionGuid);
                    execution.Status = "Failed";
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.Error = ex.Message;
                }
            });

            return ApiResponse<ExecutionResult>.Ok(new ExecutionResult
            {
                ExecutionId = executionGuid,
                PipelineName = pipeline.Name,
                Status = "Running"
            }, "工作流已开始执行");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行工作流失败");
            return ApiResponse<ExecutionResult>.Fail("执行工作流失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 查询执行状态
    /// </summary>
    [HttpGet("executions/{executionId:guid}/status")]
    public ApiResponse<ExecutionStatus> GetExecutionStatus(Guid executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            return ApiResponse<ExecutionStatus>.Fail("执行记录不存在", 404);

        return ApiResponse<ExecutionStatus>.Ok(new ExecutionStatus
        {
            ExecutionId = execution.Id,
            PipelineName = execution.PipelineName,
            Status = execution.Status,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Error = execution.Error
        });
    }

    /// <summary>
    /// 获取所有执行记录
    /// </summary>
    [HttpGet("executions")]
    public ApiResponse<PagedExecutionResult> GetExecutions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pipelineName = null)
    {
        var allExecutions = _executions.Values
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(pipelineName))
        {
            var resolvedPipelineName = _pipelineDefinitions
                .FirstOrDefault(d => string.Equals(d.Id, pipelineName, StringComparison.OrdinalIgnoreCase))?.Name
                ?? pipelineName;

            allExecutions = allExecutions
                .Where(e => string.Equals(e.PipelineName, resolvedPipelineName, StringComparison.OrdinalIgnoreCase)
                    || e.PipelineName.Contains(pipelineName, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = allExecutions.Count();
        var items = allExecutions
            .OrderByDescending(e => e.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExecutionRecord
            {
                Id = e.Id,
                PipelineName = e.PipelineName,
                Status = e.Status,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                Duration = e.CompletedAt.HasValue
                    ? (int)(e.CompletedAt.Value - e.StartedAt).TotalMilliseconds
                    : null,
                Error = e.Error
            })
            .ToList();

        return ApiResponse<PagedExecutionResult>.Ok(new PagedExecutionResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 获取工作流详情
    /// </summary>
    [HttpGet("{id}")]
    public ApiResponse<PipelineDefinition> GetPipeline(string id)
    {
        var (pipeline, definition, _) = ResolveWorkflow(id);

        if (pipeline != null)
        {
            return ApiResponse<PipelineDefinition>.Ok(new PipelineDefinition
            {
                Id = pipeline.Name,
                Name = pipeline.Name,
                Description = pipeline.Description ?? string.Empty,
                StageCount = pipeline.Stages.Count,
                Stages = pipeline.Stages.Select(s => new StageInfo
                {
                    Name = s.Name,
                    Mode = s.Mode.ToString(),
                    StepCount = s.Steps.Count
                }).ToList(),
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (definition == null)
            return ApiResponse<PipelineDefinition>.Fail("工作流不存在", 404);

        return ApiResponse<PipelineDefinition>.Ok(definition);
    }

    /// <summary>
    /// 更新工作流定义
    /// </summary>
    [HttpPut("{id}")]
    public ApiResponse<PipelineDefinition> UpdatePipeline(string id, [FromBody] UpdatePipelineRequest request)
    {
        var index = _pipelineDefinitions.FindIndex(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Name, id, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            return ApiResponse<PipelineDefinition>.Fail("工作流不存在", 404);

        var existing = _pipelineDefinitions[index];
        existing.Name = request.Name ?? existing.Name;
        existing.Description = request.Description ?? existing.Description;
        if (request.Config != null)
            existing.Config = request.Config;
        existing.UpdatedAt = DateTime.UtcNow;

        return ApiResponse<PipelineDefinition>.Ok(existing, "工作流更新成功");
    }

    /// <summary>
    /// 删除工作流定义
    /// </summary>
    [HttpDelete("{id}")]
    public ApiResponse DeletePipeline(string id)
    {
        if (_pipelineRegistry.Get(id) is not null)
        {
            return ApiResponse.Fail("系统工作流不允许删除", 400);
        }

        var removed = _pipelineDefinitions.RemoveAll(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Name, id, StringComparison.OrdinalIgnoreCase));

        return removed > 0
            ? ApiResponse.Ok("工作流删除成功")
            : ApiResponse.Fail("工作流不存在", 404);
    }

    /// <summary>
    /// 获取执行输入
    /// </summary>
    [HttpGet("executions/{executionId:guid}/input")]
    public ApiResponse<ExecutionInput> GetExecutionInput(Guid executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            return ApiResponse<ExecutionInput>.Fail("执行记录不存在", 404);

        return ApiResponse<ExecutionInput>.Ok(new ExecutionInput
        {
            ExecutionId = execution.Id,
            PipelineName = execution.PipelineName,
            Input = execution.Input ?? new Dictionary<string, object?>(),
            StartedAt = execution.StartedAt
        });
    }

    /// <summary>
    /// 获取执行结果详情
    /// </summary>
    [HttpGet("executions/{executionId:guid}/result")]
    public ApiResponse<ExecutionResultDetail> GetExecutionResult(Guid executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            return ApiResponse<ExecutionResultDetail>.Fail("执行记录不存在", 404);

        if (execution.Result == null)
            return ApiResponse<ExecutionResultDetail>.Fail("执行结果未就绪", 404);

        var result = execution.Result;
        return ApiResponse<ExecutionResultDetail>.Ok(new ExecutionResultDetail
        {
            ExecutionId = execution.Id,
            PipelineName = execution.PipelineName,
            Status = execution.Status,
            IsSuccess = result.IsSuccess,
            StageResults = result.StageResults.Select(sr => new StageResultDetail
            {
                StageName = sr.StageName,
                IsSuccess = sr.IsSuccess,
                DurationMs = (int)sr.TotalDuration.TotalMilliseconds,
                StepResults = sr.StepResults.Select(s => new StepResultDetail
                {
                    StepName = s.Output?.ContainsKey("stepName") == true ? s.Output["stepName"]?.ToString() ?? "Unknown" : "Unknown",
                    Status = s.Status.ToString(),
                    ErrorMessage = s.ErrorMessage,
                    DurationMs = (int)s.Duration.TotalMilliseconds,
                    Output = s.Output
                }).ToList()
            }).ToList(),
            TotalDurationMs = (int)result.TotalDuration.TotalMilliseconds,
            Output = result.Output,
            Error = execution.Error
        });
    }
}

public record CreatePipelineRequest(string Name, string? Description = null, Dictionary<string, object?>? Config = null);
public record UpdatePipelineRequest(string? Name = null, string? Description = null, Dictionary<string, object?>? Config = null);
public record ExecuteWorkflowRequest(Dictionary<string, object?>? Input = null);

public class PipelineDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int StageCount { get; set; }
    public List<StageInfo>? Stages { get; set; }
    public Dictionary<string, object?>? Config { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsSystem { get; set; }
}

public class StageInfo
{
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public int StepCount { get; set; }
}

public class ExecutionInput
{
    public Guid ExecutionId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public Dictionary<string, object?> Input { get; set; } = new();
    public DateTime StartedAt { get; set; }
}

public class ExecutionResultDetail
{
    public Guid ExecutionId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public List<StageResultDetail> StageResults { get; set; } = new();
    public int TotalDurationMs { get; set; }
    public IDictionary<string, object?>? Output { get; set; }
    public string? Error { get; set; }
}

public class StageResultDetail
{
    public string StageName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public int DurationMs { get; set; }
    public List<StepResultDetail> StepResults { get; set; } = new();
}

public class StepResultDetail
{
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public IDictionary<string, object>? Output { get; set; }
}

public class ExecutionResult
{
    public Guid ExecutionId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ExecutionStatus
{
    public Guid ExecutionId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}

internal class WorkflowExecution
{
    public Guid Id { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public PipelineResult? Result { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object?>? Input { get; set; }
}

public class ExecutionRecord
{
    public Guid Id { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? Duration { get; set; }
    public string? Error { get; set; }
}

public class PagedExecutionResult
{
    public IEnumerable<ExecutionRecord> Items { get; set; } = Enumerable.Empty<ExecutionRecord>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
