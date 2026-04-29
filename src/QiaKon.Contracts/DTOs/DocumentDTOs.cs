namespace QiaKon.Contracts.DTOs;

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// 文档列表项DTO
/// </summary>
public sealed record DocumentListItemDto(
    Guid Id,
    string Title,
    DocumentType Type,
    Guid DepartmentId,
    string DepartmentName,
    AccessLevel AccessLevel,
    IndexStatus IndexStatus,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedBy,
    long Size,
    JsonObject? Metadata);

/// <summary>
/// 文档详情DTO
/// </summary>
public sealed record DocumentDetailDto(
    Guid Id,
    string Title,
    string? Content,
    DocumentType Type,
    Guid DepartmentId,
    string DepartmentName,
    AccessLevel AccessLevel,
    IndexStatus IndexStatus,
    int Version,
    int? IndexVersion,
    long Size,
    JsonObject? Metadata,
    DateTime CreatedAt,
    Guid CreatedBy,
    DateTime? ModifiedAt,
    Guid? ModifiedBy);

/// <summary>
/// 创建文档请求
/// </summary>
public sealed record CreateDocumentRequestDto(
    string Title,
    string Content,
    DocumentType Type,
    Guid DepartmentId,
    AccessLevel AccessLevel,
    JsonObject? Metadata);

/// <summary>
/// 更新文档请求
/// </summary>
public sealed record UpdateDocumentRequestDto(
    string? Title,
    string? Content,
    AccessLevel? AccessLevel,
    JsonObject? Metadata);

/// <summary>
/// 文档上传表单
/// </summary>
public sealed record UploadDocumentFormDto(
    string? Title,
    string? Description,
    Guid? DepartmentId,
    AccessLevel? AccessLevel,
    string? Visibility);

/// <summary>
/// 文档分页列表响应
/// </summary>
public sealed record DocumentPagedResultDto(
    IReadOnlyList<DocumentListItemDto> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// 文档块信息DTO
/// </summary>
public sealed record ChunkInfoDto(
    Guid Id,
    int Order,
    string Content,
    string? ChunkingStrategy,
    DateTime CreatedAt);

/// <summary>
/// 文档详情（包含分块）DTO
/// </summary>
public sealed record DocumentDetailWithChunksDto(
    Guid Id,
    string Title,
    string? Content,
    DocumentType Type,
    Guid DepartmentId,
    string DepartmentName,
    AccessLevel AccessLevel,
    IndexStatus IndexStatus,
    int Version,
    int? IndexVersion,
    long Size,
    JsonObject? Metadata,
    DateTime CreatedAt,
    Guid CreatedBy,
    DateTime? ModifiedAt,
    Guid? ModifiedBy,
    IReadOnlyList<ChunkInfoDto> Chunks);

/// <summary>
/// 批量删除请求
/// </summary>
public sealed record BatchDeleteRequestDto(
    IReadOnlyList<Guid> DocumentIds);

/// <summary>
/// 批量删除响应
/// </summary>
public sealed record BatchDeleteResponseDto(
    int DeletedCount,
    int FailedCount,
    IReadOnlyList<Guid> FailedIds);

/// <summary>
/// 索引队列项DTO
/// </summary>
public sealed record IndexQueueItemDto(
    Guid DocumentId,
    string Title,
    IndexStatus Status,
    double? Progress,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    DateTime? CreatedAt);

/// <summary>
/// 索引队列状态响应
/// </summary>
public sealed record IndexQueueStatusDto(
    int PendingCount,
    int IndexingCount,
    int CompletedCount,
    int FailedCount,
    IReadOnlyList<IndexQueueItemDto> PendingItems,
    IReadOnlyList<IndexQueueItemDto> IndexingItems,
    IReadOnlyList<IndexQueueItemDto> CompletedItems,
    IReadOnlyList<IndexQueueItemDto> FailedItems);

/// <summary>
/// 索引队列扁平响应（供前端使用）
/// </summary>
public sealed record IndexQueueResponseDto(
    IReadOnlyList<IndexQueueItemDto> Items,
    int TotalCount);

/// <summary>
/// 索引统计响应
/// </summary>
public sealed record IndexStatsDto(
    long TotalDocuments,
    long TotalChunks,
    double SuccessRate,
    [property: JsonPropertyName("avgDuration")] double AverageDurationSeconds,
    long CompletedToday,
    long FailedToday,
    long PendingCount,
    long IndexingCount,
    long CompletedCount,
    long FailedCount);

/// <summary>
/// 重新解析请求
/// </summary>
public sealed record ReparseRequestDto(
    Guid DocumentId,
    string? ChunkingStrategy = null);

/// <summary>
/// 重新解析响应
/// </summary>
public sealed record ReparseResponseDto(
    Guid DocumentId,
    string Message,
    int NewChunkCount);
