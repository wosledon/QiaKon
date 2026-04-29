using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GraphsController : ControllerBase
{
    private readonly IGraphOverviewService _overviewService;
    private readonly IGraphService _graphService;

    public GraphsController(IGraphOverviewService overviewService, IGraphService graphService)
    {
        _overviewService = overviewService;
        _graphService = graphService;
    }

    /// <summary>
    /// 获取图谱概览统计
    /// </summary>
    [HttpGet]
    public ApiResponse<GraphOverviewDto> GetOverview()
    {
        var overview = _overviewService.GetOverview();
        return ApiResponse<GraphOverviewDto>.Ok(overview);
    }

    /// <summary>
    /// 获取图谱预览数据（用于可视化概览）
    /// </summary>
    /// <param name="limit">最大节点数，默认100</param>
    [HttpGet("preview")]
    public ApiResponse<GraphPreviewResultDto> GetPreview([FromQuery] int limit = 100)
    {
        if (limit <= 0) limit = 100;
        if (limit > 1000) limit = 1000; // 防止过大
        var result = _graphService.GetPreview(limit);
        return ApiResponse<GraphPreviewResultDto>.Ok(result);
    }

    /// <summary>
    /// 获取实体类型分布
    /// </summary>
    [HttpGet("stats/entity-types")]
    public ApiResponse<EntityTypeDistributionDto> GetEntityTypeDistribution()
    {
        var overview = _overviewService.GetOverview();
        return ApiResponse<EntityTypeDistributionDto>.Ok(new EntityTypeDistributionDto(overview.EntityTypeDistribution));
    }

    /// <summary>
    /// 获取关系类型分布
    /// </summary>
    [HttpGet("stats/relation-types")]
    public ApiResponse<RelationTypeDistributionDto> GetRelationTypeDistribution()
    {
        var overview = _overviewService.GetOverview();
        return ApiResponse<RelationTypeDistributionDto>.Ok(new RelationTypeDistributionDto(overview.RelationTypeDistribution));
    }

    #region Entity Management

    /// <summary>
    /// 分页获取实体列表
    /// </summary>
    [HttpGet("entities")]
    public ApiResponse<EntityPagedResultDto> GetEntities(
        [FromQuery] string? name = null,
        [FromQuery] string? type = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] bool? isPublic = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var offset = (page - 1) * pageSize;
        var result = _graphService.GetEntitiesFiltered(name, type, departmentId, isPublic, offset, pageSize);
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

    #endregion

    #region Relation Management

    /// <summary>
    /// 分页获取关系列表
    /// </summary>
    [HttpGet("relations")]
    public ApiResponse<RelationListResultDto> GetRelations(
        [FromQuery] string? type = null,
        [FromQuery] string? sourceEntityId = null,
        [FromQuery] string? targetEntityId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var offset = (page - 1) * pageSize;
        var result = _graphService.GetRelationsFiltered(type, sourceEntityId, targetEntityId, offset, pageSize);
        return ApiResponse<RelationListResultDto>.Ok(result);
    }

    /// <summary>
    /// 获取关系详情
    /// </summary>
    [HttpGet("relations/{id}")]
    public ApiResponse<RelationDetailDto> GetRelation(string id)
    {
        var relation = _graphService.GetRelationDetail(id);
        return relation is null
            ? ApiResponse<RelationDetailDto>.Fail("关系不存在", 404)
            : ApiResponse<RelationDetailDto>.Ok(relation);
    }

    /// <summary>
    /// 创建关系
    /// </summary>
    [HttpPost("relations")]
    public ApiResponse<GraphRelationDto> CreateRelation([FromBody] CreateRelationRequestDto request)
    {
        if (!_graphService.GetEntity(request.SourceId)?.Entity.IsPublic ?? true)
        {
            var userId = GetCurrentUserId();
            var relation = _graphService.CreateRelation(request, userId);
            return ApiResponse<GraphRelationDto>.Ok(relation, "关系创建成功");
        }

        var userIdForCreate = GetCurrentUserId();
        var createdRelation = _graphService.CreateRelation(request, userIdForCreate);
        return ApiResponse<GraphRelationDto>.Ok(createdRelation, "关系创建成功");
    }

    /// <summary>
    /// 更新关系
    /// </summary>
    [HttpPut("relations/{id}")]
    public ApiResponse<GraphRelationDto> UpdateRelation(string id, [FromBody] UpdateRelationRequestDto request)
    {
        var relation = _graphService.UpdateRelation(id, request);
        return relation is null
            ? ApiResponse<GraphRelationDto>.Fail("关系不存在", 404)
            : ApiResponse<GraphRelationDto>.Ok(relation, "关系更新成功");
    }

    /// <summary>
    /// 删除关系
    /// </summary>
    [HttpDelete("relations/{id}")]
    public ApiResponse DeleteRelation(string id)
    {
        var result = _graphService.DeleteRelation(id);
        return result
            ? ApiResponse.Ok("关系删除成功")
            : ApiResponse.Fail("删除失败，关系可能不存在", 404);
    }

    #endregion

    #region Graph Query

    /// <summary>
    /// 路径查询
    /// </summary>
    [HttpPost("query/path")]
    public ApiResponse<PathQueryResultDto> QueryPath([FromBody] PathQueryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceEntityId) || string.IsNullOrWhiteSpace(request.TargetEntityId))
        {
            return ApiResponse<PathQueryResultDto>.Fail("源实体ID和目标实体ID不能为空", 400);
        }

        var result = _graphService.FindPaths(
            request.SourceEntityId,
            request.TargetEntityId,
            request.MaxPaths,
            request.MaxHops);

        return ApiResponse<PathQueryResultDto>.Ok(result);
    }

    /// <summary>
    /// 多跳推理查询
    /// </summary>
    [HttpPost("query/multi-hop")]
    public ApiResponse<MultiHopQueryResultDto> QueryMultiHop([FromBody] MultiHopQueryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.StartEntityId))
        {
            return ApiResponse<MultiHopQueryResultDto>.Fail("起始实体ID不能为空", 400);
        }

        if (request.MaxHops < 1 || request.MaxHops > 10)
        {
            return ApiResponse<MultiHopQueryResultDto>.Fail("跳数必须在1-10之间", 400);
        }

        var result = _graphService.MultiHopQuery(request.StartEntityId, request.MaxHops, request.RelationTypes);
        return ApiResponse<MultiHopQueryResultDto>.Ok(result);
    }

    /// <summary>
    /// 邻居查询
    /// </summary>
    [HttpPost("query/neighbors")]
    public ApiResponse<NeighborsQueryResultDto> QueryNeighbors([FromBody] NeighborsQueryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EntityId))
        {
            return ApiResponse<NeighborsQueryResultDto>.Fail("实体ID不能为空", 400);
        }

        var direction = request.Direction?.ToLowerInvariant() ?? "both";
        if (direction != "outgoing" && direction != "incoming" && direction != "both")
        {
            return ApiResponse<NeighborsQueryResultDto>.Fail("direction必须是outgoing/incoming/both之一", 400);
        }

        var result = _graphService.FindNeighbors(request.EntityId, direction, request.Limit);
        return ApiResponse<NeighborsQueryResultDto>.Ok(result);
    }

    /// <summary>
    /// 聚合查询
    /// </summary>
    [HttpPost("query/aggregate")]
    public ApiResponse<AggregateQueryResultDto> QueryAggregate([FromBody] AggregateQueryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.GroupBy))
        {
            return ApiResponse<AggregateQueryResultDto>.Fail("groupBy不能为空", 400);
        }

        if (request.GroupBy != "type" && request.GroupBy != "department")
        {
            return ApiResponse<AggregateQueryResultDto>.Fail("groupBy必须是type或department", 400);
        }

        var result = _graphService.AggregateQuery(request.GroupBy, request.Filters);
        return ApiResponse<AggregateQueryResultDto>.Ok(result);
    }

    #endregion

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
