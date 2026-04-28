using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GraphController : ControllerBase
{
    private readonly IGraphService _graphService;
    private readonly ILogger<GraphController> _logger;

    public GraphController(IGraphService graphService, ILogger<GraphController> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    /// <summary>
    /// 获取实体列表
    /// </summary>
    [HttpGet("entities")]
    public ApiResponse<EntityPagedResultDto> GetEntities(
        [FromQuery] string? label = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        var result = _graphService.GetEntities(label, offset, limit);
        return ApiResponse<EntityPagedResultDto>.Ok(result);
    }

    /// <summary>
    /// 获取实体详情
    /// </summary>
    [HttpGet("entities/{id}")]
    public ApiResponse<EntityDetailDto> GetEntity(string id)
    {
        var entity = _graphService.GetEntity(id);
        return entity is null
            ? ApiResponse<EntityDetailDto>.Fail("实体不存在", 404)
            : ApiResponse<EntityDetailDto>.Ok(entity);
    }

    /// <summary>
    /// 创建实体
    /// </summary>
    [HttpPost("entities")]
    public ApiResponse<GraphEntityDto> CreateEntity([FromBody] CreateEntityRequestDto request)
    {
        var userId = GetCurrentUserId();
        var entity = _graphService.CreateEntity(request, userId);
        _logger.LogInformation("Entity created: {Id} - {Name} by user {UserId}", entity.Id, entity.Name, userId);
        return ApiResponse<GraphEntityDto>.Ok(entity, "实体创建成功");
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    [HttpPut("entities/{id}")]
    public ApiResponse<GraphEntityDto> UpdateEntity(string id, [FromBody] UpdateEntityRequestDto request)
    {
        var entity = _graphService.UpdateEntity(id, request);
        return entity is null
            ? ApiResponse<GraphEntityDto>.Fail("实体不存在", 404)
            : ApiResponse<GraphEntityDto>.Ok(entity, "实体更新成功");
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    [HttpDelete("entities/{id}")]
    public ApiResponse DeleteEntity(string id)
    {
        var result = _graphService.DeleteEntity(id);
        return result
            ? ApiResponse.Ok("实体删除成功")
            : ApiResponse.Fail("删除失败，实体可能不存在", 404);
    }

    /// <summary>
    /// 获取关系列表
    /// </summary>
    [HttpGet("relations")]
    public ApiResponse<RelationListResultDto> GetRelations(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        [FromQuery] string? type = null)
    {
        var result = _graphService.GetRelations(offset, limit, type);
        return ApiResponse<RelationListResultDto>.Ok(result);
    }

    /// <summary>
    /// 创建关系
    /// </summary>
    [HttpPost("relations")]
    public ApiResponse<GraphRelationDto> CreateRelation([FromBody] CreateRelationRequestDto request)
    {
        var userId = GetCurrentUserId();
        var relation = _graphService.CreateRelation(request, userId);
        _logger.LogInformation("Relation created: {Id} ({SourceId} -> {TargetId})", relation.Id, relation.SourceId, relation.TargetId);
        return ApiResponse<GraphRelationDto>.Ok(relation, "关系创建成功");
    }

    /// <summary>
    /// 图谱查询
    /// </summary>
    [HttpPost("query")]
    public ApiResponse<GraphQueryResponseDto> Query([FromBody] GraphQueryRequestDto request)
    {
        var results = _graphService.Query(request);
        return ApiResponse<GraphQueryResponseDto>.Ok(results);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return Guid.Empty;
    }
}
