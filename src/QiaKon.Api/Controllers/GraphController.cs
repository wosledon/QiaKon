using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts;
using QiaKon.Graph.Engine;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GraphController : ControllerBase
{
    private readonly IGraphEngine _graphEngine;
    private readonly ILogger<GraphController> _logger;

    public GraphController(IGraphEngine graphEngine, ILogger<GraphController> logger)
    {
        _graphEngine = graphEngine;
        _logger = logger;
    }

    /// <summary>
    /// 实体列表
    /// </summary>
    [HttpGet("entities")]
    public async Task<ApiResponse<PagedResult<GraphNode>>> GetEntities(
        [FromQuery] string? label = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20)
    {
        try
        {
            if (string.IsNullOrEmpty(label))
            {
                // 返回所有标签的实体（模拟）
                var nodes = await _graphEngine.GetNodesByLabelAsync("Entity", offset, limit);
                return ApiResponse<PagedResult<GraphNode>>.Ok(new PagedResult<GraphNode>
                {
                    Items = nodes,
                    Total = nodes.Count,
                    Offset = offset,
                    Limit = limit
                });
            }

            var result = await _graphEngine.GetNodesByLabelAsync(label, offset, limit);
            var total = await _graphEngine.CountNodesByLabelAsync(label);

            return ApiResponse<PagedResult<GraphNode>>.Ok(new PagedResult<GraphNode>
            {
                Items = result,
                Total = total,
                Offset = offset,
                Limit = limit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取实体列表失败");
            return ApiResponse<PagedResult<GraphNode>>.Fail("获取实体列表失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 创建实体
    /// </summary>
    [HttpPost("entities")]
    public async Task<ApiResponse<GraphNode>> CreateEntity([FromBody] CreateEntityRequest request)
    {
        try
        {
            var properties = new Dictionary<string, object?>();
            if (request.Properties != null)
            {
                foreach (var prop in request.Properties)
                {
                    properties[prop.Key] = prop.Value;
                }
            }
            properties["name"] = request.Name;
            properties["type"] = request.Type;

            var node = await _graphEngine.CreateNodeAsync(request.Type, properties);
            return ApiResponse<GraphNode>.Ok(node, "实体创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建实体失败");
            return ApiResponse<GraphNode>.Fail("创建实体失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 实体详情（含邻居）
    /// </summary>
    [HttpGet("entities/{id}")]
    public async Task<ApiResponse<EntityDetail>> GetEntity(string id)
    {
        try
        {
            var node = await _graphEngine.GetNodeAsync(id);
            if (node == null)
                return ApiResponse<EntityDetail>.Fail("实体不存在", 404);

            var edges = await _graphEngine.GetEdgesByNodeAsync(id);
            var neighbors = new List<NeighborInfo>();

            foreach (var edge in edges)
            {
                var neighborId = edge.SourceNodeId == id ? edge.TargetNodeId : edge.SourceNodeId;
                var neighbor = await _graphEngine.GetNodeAsync(neighborId);
                if (neighbor != null)
                {
                    neighbors.Add(new NeighborInfo
                    {
                        Node = neighbor,
                        Relation = edge.Label,
                        Direction = edge.SourceNodeId == id ? "outgoing" : "incoming"
                    });
                }
            }

            return ApiResponse<EntityDetail>.Ok(new EntityDetail
            {
                Node = node,
                Neighbors = neighbors,
                EdgeCount = edges.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取实体详情失败");
            return ApiResponse<EntityDetail>.Fail("获取实体详情失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 更新实体
    /// </summary>
    [HttpPut("entities/{id}")]
    public async Task<ApiResponse<GraphNode>> UpdateEntity(string id, [FromBody] UpdateEntityRequest request)
    {
        try
        {
            var properties = new Dictionary<string, object?>();
            if (request.Name != null) properties["name"] = request.Name;
            if (request.Type != null) properties["type"] = request.Type;
            if (request.Properties != null)
            {
                foreach (var prop in request.Properties)
                {
                    properties[prop.Key] = prop.Value;
                }
            }

            var node = await _graphEngine.UpdateNodeAsync(id, properties);
            return ApiResponse<GraphNode>.Ok(node, "实体更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新实体失败");
            return ApiResponse<GraphNode>.Fail("更新实体失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 删除实体
    /// </summary>
    [HttpDelete("entities/{id}")]
    public async Task<ApiResponse> DeleteEntity(string id)
    {
        try
        {
            var result = await _graphEngine.DeleteNodeAsync(id);
            return result
                ? ApiResponse.Ok("实体删除成功")
                : ApiResponse.Fail("删除失败，实体可能不存在", 404);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除实体失败");
            return ApiResponse.Fail("删除实体失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 创建关系
    /// </summary>
    [HttpPost("relations")]
    public async Task<ApiResponse<GraphEdge>> CreateRelation([FromBody] CreateRelationRequest request)
    {
        try
        {
            var properties = new Dictionary<string, object?>();
            if (request.Properties != null)
            {
                foreach (var prop in request.Properties)
                {
                    properties[prop.Key] = prop.Value;
                }
            }

            var edge = await _graphEngine.CreateEdgeAsync(
                request.SourceId,
                request.TargetId,
                request.Type,
                properties);

            return ApiResponse<GraphEdge>.Ok(edge, "关系创建成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建关系失败");
            return ApiResponse<GraphEdge>.Fail("创建关系失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 路径查询
    /// </summary>
    [HttpGet("query/path")]
    public async Task<ApiResponse<PathResult>> QueryPath(
        [FromQuery] string startId,
        [FromQuery] string endId,
        [FromQuery] string? edgeLabel = null)
    {
        try
        {
            var path = await _graphEngine.ShortestPathAsync(startId, endId, edgeLabel);
            return ApiResponse<PathResult>.Ok(new PathResult
            {
                Path = path,
                Length = path.Count - 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "路径查询失败");
            return ApiResponse<PathResult>.Fail("路径查询失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 邻居查询
    /// </summary>
    [HttpGet("query/neighbors/{entityId}")]
    public async Task<ApiResponse<NeighborsResult>> QueryNeighbors(
        string entityId,
        [FromQuery] int maxDepth = 1,
        [FromQuery] string? direction = null)
    {
        try
        {
            var edges = await _graphEngine.GetEdgesByNodeAsync(entityId, direction);
            var neighbors = new List<NeighborNode>();

            foreach (var edge in edges)
            {
                var neighborId = edge.SourceNodeId == entityId ? edge.TargetNodeId : edge.SourceNodeId;
                var neighbor = await _graphEngine.GetNodeAsync(neighborId);
                if (neighbor != null)
                {
                    neighbors.Add(new NeighborNode
                    {
                        Node = neighbor,
                        Relation = edge.Label,
                        Distance = 1
                    });
                }
            }

            return ApiResponse<NeighborsResult>.Ok(new NeighborsResult
            {
                CenterId = entityId,
                Neighbors = neighbors,
                TotalCount = neighbors.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邻居查询失败");
            return ApiResponse<NeighborsResult>.Fail("邻居查询失败: " + ex.Message, 500);
        }
    }
}

public record CreateEntityRequest(string Name, string Type, Dictionary<string, object?>? Properties = null);
public record UpdateEntityRequest(string? Name, string? Type, Dictionary<string, object?>? Properties = null);
public record CreateRelationRequest(string SourceId, string TargetId, string Type, Dictionary<string, object?>? Properties = null);

public class EntityDetail
{
    public GraphNode Node { get; set; } = null!;
    public List<NeighborInfo> Neighbors { get; set; } = new();
    public long EdgeCount { get; set; }
}

public class NeighborInfo
{
    public GraphNode Node { get; set; } = null!;
    public string Relation { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // incoming/outgoing
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public long Total { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
}

public class PathResult
{
    public IReadOnlyList<string> Path { get; set; } = Array.Empty<string>();
    public int Length { get; set; }
}

public class NeighborsResult
{
    public string CenterId { get; set; } = string.Empty;
    public List<NeighborNode> Neighbors { get; set; } = new();
    public int TotalCount { get; set; }
}

public class NeighborNode
{
    public GraphNode Node { get; set; } = null!;
    public string Relation { get; set; } = string.Empty;
    public int Distance { get; set; }
}
