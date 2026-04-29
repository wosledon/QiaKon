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
                Name = name,
                Description = pipeline?.Description ?? string.Empty,
                StageCount = pipeline?.Stages.Count ?? 0
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
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                Config = request.Config ?? new Dictionary<string, object>(),
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
            var pipeline = _pipelineRegistry.Get(id);
            if (pipeline == null)
            {
                var definition = _pipelineDefinitions.FirstOrDefault(d => d.Name == id);
                if (definition == null)
                    return ApiResponse<ExecutionResult>.Fail("工作流不存在", 404);

                // 使用 mock 执行
                var executionId = Guid.NewGuid();
                var mockExecution = new WorkflowExecution
                {
                    Id = executionId,
                    PipelineName = id,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow
                };
                _executions[executionId] = mockExecution;

                // 模拟异步执行
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    mockExecution.Status = "Completed";
                    mockExecution.CompletedAt = DateTime.UtcNow;
                    mockExecution.Result = PipelineResult.Success(
                        id,
                        new List<StageResult>(),
                        TimeSpan.FromMilliseconds(100),
                        new Dictionary<string, object?> { ["output"] = $"Workflow {id} executed successfully" });
                });

                return ApiResponse<ExecutionResult>.Ok(new ExecutionResult
                {
                    ExecutionId = executionId,
                    PipelineName = id,
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
                PipelineName = id,
                Status = "Running",
                StartedAt = DateTime.UtcNow
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
                PipelineName = id,
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
            allExecutions = allExecutions
                .Where(e => e.PipelineName.Contains(pipelineName, StringComparison.OrdinalIgnoreCase));
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
    /// 删除工作流定义
    /// </summary>
    [HttpDelete("{id:guid}")]
    public ApiResponse DeletePipeline(Guid id)
    {
        var removed = _pipelineDefinitions.RemoveAll(d => d.Id == id);
        return removed > 0
            ? ApiResponse.Ok("工作流删除成功")
            : ApiResponse.Fail("工作流不存在", 404);
    }
}

public record CreatePipelineRequest(string Name, string? Description = null, Dictionary<string, object?>? Config = null);
public record ExecuteWorkflowRequest(Dictionary<string, object?>? Input = null);

public class PipelineDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int StageCount { get; set; }
    public Dictionary<string, object>? Config { get; set; }
    public DateTime CreatedAt { get; set; }
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
