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
