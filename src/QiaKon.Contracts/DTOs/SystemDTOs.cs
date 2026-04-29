namespace QiaKon.Contracts.DTOs;

/// <summary>
/// 部门DTO
/// </summary>
public sealed record DepartmentDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? ParentName,
    int MemberCount,
    DateTime CreatedAt);

/// <summary>
/// 创建部门请求
/// </summary>
public sealed record CreateDepartmentDto(
    string Name,
    Guid? ParentId);

/// <summary>
/// 更新部门请求
/// </summary>
public sealed record UpdateDepartmentDto(
    string? Name,
    Guid? ParentId);

/// <summary>
/// 角色DTO
/// </summary>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string Description,
    bool IsSystem,
    int UserCount,
    PermissionMatrixDto? Permissions);

/// <summary>
/// 权限矩阵DTO
/// </summary>
public sealed record PermissionMatrixDto(
    bool CanReadPublicDocuments,
    bool CanWritePublicDocuments,
    bool CanDeletePublicDocuments,
    bool CanReadDepartmentDocuments,
    bool CanWriteDepartmentDocuments,
    bool CanDeleteDepartmentDocuments,
    bool CanReadAllDocuments,
    bool CanWriteAllDocuments,
    bool CanDeleteAllDocuments,
    bool CanManageUsers,
    bool CanManageRoles,
    bool CanManageDepartments,
    bool CanViewAuditLogs,
    bool CanManageSystemConfig);

/// <summary>
/// 创建角色请求
/// </summary>
public sealed record CreateRoleDto(
    string Name,
    string Description,
    PermissionMatrixDto? Permissions);

/// <summary>
/// 更新角色请求
/// </summary>
public sealed record UpdateRoleDto(
    string? Name,
    string? Description,
    PermissionMatrixDto? Permissions);

/// <summary>
/// 用户列表项DTO
/// </summary>
public sealed record UserListItemDto(
    Guid Id,
    string Username,
    string Email,
    Guid DepartmentId,
    string DepartmentName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt);

/// <summary>
/// 创建用户请求
/// </summary>
public sealed record CreateUserDto(
    string Username,
    string Email,
    string InitialPassword,
    Guid DepartmentId,
    UserRole Role);

/// <summary>
/// 更新用户请求
/// </summary>
public sealed record UpdateUserDto(
    string? Email,
    Guid? DepartmentId,
    UserRole? Role,
    bool? IsActive);

/// <summary>
/// 修改密码请求
/// </summary>
public sealed record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);

/// <summary>
/// 更新个人资料请求
/// </summary>
public sealed record UpdateProfileDto(
    string? Email);

/// <summary>
/// LLM供应商DTO
/// </summary>
public sealed record LlmProviderDto(
    Guid Id,
    string Name,
    LlmInterfaceType InterfaceType,
    string BaseUrl,
    string? ApiKey,
    int TimeoutSeconds,
    int RetryCount,
    bool HasModels,
    IReadOnlyList<LlmModelDto> Models);

/// <summary>
/// LLM接口类型
/// </summary>
public enum LlmInterfaceType
{
    OpenAI,
    Anthropic
}

/// <summary>
/// LLM模型DTO
/// </summary>
public sealed record LlmModelDto(
    Guid Id,
    Guid ProviderId,
    string Name,
    string ActualModelName,
    LlmModelType ModelType,
    int? VectorDimensions,
    int? MaxTokens,
    bool IsEnabled,
    bool IsDefault);

/// <summary>
/// LLM模型类型
/// </summary>
public enum LlmModelType
{
    Inference,
    Embedding
}

/// <summary>
/// 创建LLM供应商请求
/// </summary>
public sealed record CreateLlmProviderDto(
    string Name,
    LlmInterfaceType InterfaceType,
    string BaseUrl,
    string? ApiKey,
    int TimeoutSeconds = 60,
    int RetryCount = 3);

/// <summary>
/// 创建LLM模型请求
/// </summary>
public sealed record CreateLlmModelDto(
    Guid ProviderId,
    string Name,
    string ActualModelName,
    LlmModelType ModelType,
    int? VectorDimensions,
    int? MaxTokens,
    bool SetAsDefault = false);

/// <summary>
/// 更新LLM模型请求
/// </summary>
public sealed record UpdateLlmModelDto(
    string? Name,
    string? ActualModelName,
    int? VectorDimensions,
    int? MaxTokens,
    bool? SetAsDefault = false);

/// <summary>
/// 系统配置DTO
/// </summary>
public sealed record SystemConfigDto(
    string DefaultChunkingStrategy,
    int ChunkSize,
    int ChunkOverlap,
    int DefaultVectorDimensions,
    string CacheStrategy,
    int CacheExpirationMinutes,
    string PromptTemplate);

/// <summary>
/// 更新系统配置请求
/// </summary>
public sealed record UpdateSystemConfigDto(
    string? DefaultChunkingStrategy,
    int? ChunkSize,
    int? ChunkOverlap,
    int? DefaultVectorDimensions,
    string? CacheStrategy,
    int? CacheExpirationMinutes,
    string? PromptTemplate);

/// <summary>
/// 连接器DTO
/// </summary>
public sealed record ConnectorDto(
    Guid Id,
    string Name,
    ConnectorType Type,
    ConnectorState State,
    string? BaseUrl,
    string? ConnectionString,
    DateTime? LastHealthCheck,
    IReadOnlyList<ConnectorEndpointDto> Endpoints);

/// <summary>
/// 连接器类型
/// </summary>
public enum ConnectorType
{
    Http,
    Npgsql,
    Redis,
    MessageQueue,
    Custom
}

/// <summary>
/// 连接器状态
/// </summary>
public enum ConnectorState
{
    Disconnected,
    Connecting,
    Connected,
    Healthy,
    Unhealthy,
    Closed
}

/// <summary>
/// 连接器端点DTO
/// </summary>
public sealed record ConnectorEndpointDto(
    string Name,
    string Url,
    string Method);

/// <summary>
/// 创建连接器请求
/// </summary>
public sealed record CreateConnectorDto(
    string Name,
    ConnectorType Type,
    string? BaseUrl,
    string? ConnectionString,
    IReadOnlyList<ConnectorEndpointDto>? Endpoints);

/// <summary>
/// 审计日志DTO
/// </summary>
public sealed record AuditLogDto(
    Guid Id,
    Guid UserId,
    string Username,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    string? ResourceName,
    string Result,
    string? IpAddress,
    string? Details,
    DateTime Timestamp);

/// <summary>
/// 审计日志分页结果
/// </summary>
public sealed record AuditLogPagedResultDto(
    IReadOnlyList<AuditLogDto> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// 通用分页结果
/// </summary>
public sealed record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// 对话历史DTO
/// </summary>
public sealed record ConversationHistoryDto(
    Guid Id,
    string Title,
    int MessageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// 对话详情DTO
/// </summary>
public sealed record ConversationDetailDto(
    Guid Id,
    string Title,
    IReadOnlyList<ChatMessageDto> Messages,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// 聊天消息DTO
/// </summary>
public sealed record ChatMessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt,
    IReadOnlyList<RagSourceDto>? Sources);
