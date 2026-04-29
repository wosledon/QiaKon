namespace QiaKon.Contracts.DTOs;

using System.Text.Json.Nodes;

/// <summary>
/// 图谱实体DTO
/// </summary>
public sealed record GraphEntityDto(
    string Id,
    string Name,
    string Type,
    Guid DepartmentId,
    string DepartmentName,
    bool IsPublic,
    JsonObject Properties,
    DateTime CreatedAt,
    Guid CreatedBy);

/// <summary>
/// 图谱关系DTO
/// </summary>
public sealed record GraphRelationDto(
    string Id,
    string SourceId,
    string SourceName,
    string TargetId,
    string TargetName,
    string Type,
    Guid DepartmentId,
    JsonObject Properties,
    DateTime CreatedAt,
    Guid CreatedBy);

/// <summary>
/// 创建实体请求
/// </summary>
public sealed record CreateEntityRequestDto(
    string Name,
    string Type,
    Guid? DepartmentId,
    AccessLevel? AccessLevel,
    Dictionary<string, object?>? Properties);

/// <summary>
/// 更新实体请求
/// </summary>
public sealed record UpdateEntityRequestDto(
    string? Name,
    string? Type,
    Dictionary<string, object?>? Properties);

/// <summary>
/// 创建关系请求
/// </summary>
public sealed record CreateRelationRequestDto(
    string SourceId,
    string TargetId,
    string Type,
    Dictionary<string, object?>? Properties);

/// <summary>
/// 更新关系请求
/// </summary>
public sealed record UpdateRelationRequestDto(
    string? Type,
    Dictionary<string, object?>? Properties);

/// <summary>
/// 实体详情（含邻居）
/// </summary>
public sealed record EntityDetailDto(
    GraphEntityDto Entity,
    IReadOnlyList<NeighborDto> Neighbors,
    int TotalCount);

/// <summary>
/// 邻居信息
/// </summary>
public sealed record NeighborDto(
    GraphEntityDto Node,
    string Relation,
    string Direction);

/// <summary>
/// 图谱实体分页结果
/// </summary>
public sealed record EntityPagedResultDto(
    IReadOnlyList<GraphEntityDto> Items,
    long TotalCount,
    int Offset,
    int Limit);

/// <summary>
/// 图谱关系列表结果
/// </summary>
public sealed record RelationListResultDto(
    IReadOnlyList<GraphRelationDto> Items,
    long TotalCount);

/// <summary>
/// 路径查询结果
/// </summary>
public sealed record GraphPathDto(
    IReadOnlyList<GraphEntityDto> Nodes,
    IReadOnlyList<GraphRelationDto> Edges,
    int TotalHops);

/// <summary>
/// 邻居查询结果
/// </summary>
public sealed record NeighborsQueryResultDto(
    string CenterId,
    IReadOnlyList<NeighborDto> Neighbors,
    int TotalCount);

/// <summary>
/// 图谱查询请求
/// </summary>
public sealed record GraphQueryRequestDto(
    string? StartEntityId,
    string? EndEntityId,
    string? RelationType,
    int MaxHops = 3);

/// <summary>
/// 图谱查询响应
/// </summary>
public sealed record GraphQueryResponseDto(
    IReadOnlyList<GraphPathDto> Paths);

/// <summary>
/// 路径查询请求
/// </summary>
public sealed record PathQueryRequestDto(
    string SourceEntityId,
    string TargetEntityId,
    int MaxPaths = 5,
    int MaxHops = 5);

/// <summary>
/// 路径查询结果
/// </summary>
public sealed record PathQueryResultDto(
    IReadOnlyList<GraphPathDto> Paths,
    int TotalPaths);

/// <summary>
/// 多跳推理请求
/// </summary>
public sealed record MultiHopQueryRequestDto(
    string StartEntityId,
    int MaxHops = 3,
    IReadOnlyList<string>? RelationTypes = null);

/// <summary>
/// 多跳推理结果
/// </summary>
public sealed record MultiHopQueryResultDto(
    string StartEntityId,
    IReadOnlyList<ReachableEntityDto> ReachableEntities,
    int TotalCount);

/// <summary>
/// 可达实体
/// </summary>
public sealed record ReachableEntityDto(
    GraphEntityDto Entity,
    int MinHops,
    IReadOnlyList<string> PathRelations);

/// <summary>
/// 邻居查询请求
/// </summary>
public sealed record NeighborsQueryRequestDto(
    string EntityId,
    string Direction = "both",
    int Limit = 50);

/// <summary>
/// 聚合查询请求
/// </summary>
public sealed record AggregateQueryRequestDto(
    string GroupBy,
    AggregateFilterDto? Filters = null);

/// <summary>
/// 聚合查询过滤器
/// </summary>
public sealed record AggregateFilterDto(
    IReadOnlyList<string>? EntityTypes = null,
    IReadOnlyList<string>? RelationTypes = null,
    Guid? DepartmentId = null,
    bool? IsPublic = null);

/// <summary>
/// 聚合查询结果
/// </summary>
public sealed record AggregateQueryResultDto(
    IReadOnlyList<AggregateGroupDto> Groups,
    long TotalCount);

/// <summary>
/// 聚合分组
/// </summary>
public sealed record AggregateGroupDto(
    string Key,
    long Count,
    double Percentage);

/// <summary>
/// 关系详情（含源和目标信息）
/// </summary>
public sealed record RelationDetailDto(
    GraphRelationDto Relation,
    GraphEntityDto SourceEntity,
    GraphEntityDto TargetEntity);

/// <summary>
/// 实体类型分布
/// </summary>
public sealed record EntityTypeDistributionDto(
    IReadOnlyDictionary<string, long> Distribution);

/// <summary>
/// 关系类型分布
/// </summary>
public sealed record RelationTypeDistributionDto(
    IReadOnlyDictionary<string, long> Distribution);

#region Graph Preview DTOs

/// <summary>
/// 图谱预览节点（适用于可视化）
/// </summary>
public sealed record GraphPreviewNodeDto(
    string Id,
    string Name,
    string Type,
    string DepartmentName,
    bool IsPublic,
    int Degree);

/// <summary>
/// 图谱预览边（适用于可视化）
/// </summary>
public sealed record GraphPreviewEdgeDto(
    string Id,
    string SourceId,
    string TargetId,
    string Type);

/// <summary>
/// 图谱预览结果
/// </summary>
public sealed record GraphPreviewResultDto(
    IReadOnlyList<GraphPreviewNodeDto> Nodes,
    IReadOnlyList<GraphPreviewEdgeDto> Edges,
    long TotalNodeCount,
    long TotalEdgeCount);

#endregion
