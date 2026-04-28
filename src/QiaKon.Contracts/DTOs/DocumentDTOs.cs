namespace QiaKon.Contracts.DTOs;

using System.Text.Json.Nodes;

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
    AccessLevel? AccessLevel);

/// <summary>
/// 文档分页列表响应
/// </summary>
public sealed record DocumentPagedResultDto(
    IReadOnlyList<DocumentListItemDto> Items,
    long TotalCount,
    int Page,
    int PageSize);
