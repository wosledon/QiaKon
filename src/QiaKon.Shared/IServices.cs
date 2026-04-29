using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace QiaKon.Shared;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    LoginResponseDto? Login(LoginRequestDto request);

    /// <summary>
    /// 验证Token
    /// </summary>
    (bool IsValid, Guid UserId, UserRole Role) ValidateToken(string token);

    /// <summary>
    /// 获取用户信息
    /// </summary>
    UserDto? GetUserById(Guid userId);
}

/// <summary>
/// 文档服务接口
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// 获取文档列表（支持搜索和排序）
    /// </summary>
    DocumentPagedResultDto GetDocuments(
        int page,
        int pageSize,
        Guid? departmentId = null,
        IndexStatus? status = null,
        string? searchTitle = null,
        string sortBy = "createdAt",
        bool sortDescending = true);

    /// <summary>
    /// 获取文档详情
    /// </summary>
    DocumentDetailDto? GetDocument(Guid id);

    /// <summary>
    /// 获取文档详情（包含分块信息）
    /// </summary>
    DocumentDetailWithChunksDto? GetDocumentWithChunks(Guid id);

    /// <summary>
    /// 创建文档
    /// </summary>
    DocumentDetailDto CreateDocument(CreateDocumentRequestDto request, Guid userId);

    /// <summary>
    /// 上传文档
    /// </summary>
    Task<DocumentDetailDto> UploadDocumentAsync(IFormFile file, UploadDocumentFormDto form, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新文档
    /// </summary>
    DocumentDetailDto? UpdateDocument(Guid id, UpdateDocumentRequestDto request, Guid userId);

    /// <summary>
    /// 删除文档
    /// </summary>
    bool DeleteDocument(Guid id);

    /// <summary>
    /// 批量删除文档
    /// </summary>
    BatchDeleteResponseDto BatchDeleteDocuments(IReadOnlyList<Guid> documentIds);

    /// <summary>
    /// 获取文档下载信息（返回文件路径和文件名）
    /// </summary>
    (string FilePath, string FileName)? GetDocumentDownloadInfo(Guid id);

    /// <summary>
    /// 重建文档索引
    /// </summary>
    ReindexResponseDto Reindex(Guid? documentId);

    /// <summary>
    /// 获取索引队列状态
    /// </summary>
    IndexQueueStatusDto GetIndexQueueStatus();

    /// <summary>
    /// 获取全部索引队列项（供扁平列表展示）
    /// </summary>
    IndexQueueResponseDto GetAllIndexQueueItems();

    /// <summary>
    /// 批量重试失败任务
    /// </summary>
    ReindexResponseDto RetryFailedIndexing();

    /// <summary>
    /// 获取索引统计
    /// </summary>
    IndexStatsDto GetIndexStats();

    /// <summary>
    /// 重新解析文档
    /// </summary>
    ReparseResponseDto ReparseDocument(Guid documentId, string? chunkingStrategy = null);
}

/// <summary>
/// 图谱服务接口
/// </summary>
public interface IGraphService
{
    /// <summary>
    /// 获取实体列表
    /// </summary>
    EntityPagedResultDto GetEntities(string? label, int offset, int limit);

    /// <summary>
    /// 获取实体列表（支持筛选）
    /// </summary>
    EntityPagedResultDto GetEntitiesFiltered(string? name, string? type, Guid? departmentId, bool? isPublic, int offset, int limit);

    /// <summary>
    /// 获取实体详情
    /// </summary>
    EntityDetailDto? GetEntity(string id);

    /// <summary>
    /// 创建实体
    /// </summary>
    GraphEntityDto CreateEntity(CreateEntityRequestDto request, Guid userId);

    /// <summary>
    /// 更新实体
    /// </summary>
    GraphEntityDto? UpdateEntity(string id, UpdateEntityRequestDto request);

    /// <summary>
    /// 删除实体
    /// </summary>
    bool DeleteEntity(string id);

    /// <summary>
    /// 获取关系列表
    /// </summary>
    RelationListResultDto GetRelations(int offset, int limit, string? type = null);

    /// <summary>
    /// 获取关系列表（支持筛选）
    /// </summary>
    RelationListResultDto GetRelationsFiltered(string? type, string? sourceEntityId, string? targetEntityId, int offset, int limit);

    /// <summary>
    /// 获取关系详情
    /// </summary>
    RelationDetailDto? GetRelationDetail(string id);

    /// <summary>
    /// 创建关系
    /// </summary>
    GraphRelationDto CreateRelation(CreateRelationRequestDto request, Guid userId);

    /// <summary>
    /// 更新关系
    /// </summary>
    GraphRelationDto? UpdateRelation(string id, UpdateRelationRequestDto request);

    /// <summary>
    /// 删除关系
    /// </summary>
    bool DeleteRelation(string id);

    /// <summary>
    /// 图谱查询
    /// </summary>
    GraphQueryResponseDto Query(GraphQueryRequestDto request);

    /// <summary>
    /// BFS路径查询
    /// </summary>
    PathQueryResultDto FindPaths(string sourceId, string targetId, int maxPaths, int maxHops);

    /// <summary>
    /// 多跳推理查询
    /// </summary>
    MultiHopQueryResultDto MultiHopQuery(string startId, int maxHops, IReadOnlyList<string>? relationTypes = null);

    /// <summary>
    /// 邻居查询
    /// </summary>
    NeighborsQueryResultDto FindNeighbors(string entityId, string direction, int limit);

    /// <summary>
    /// 聚合查询
    /// </summary>
    AggregateQueryResultDto AggregateQuery(string groupBy, AggregateFilterDto? filters = null);

    /// <summary>
    /// 获取图谱预览数据（用于可视化概览）
    /// </summary>
    GraphPreviewResultDto GetPreview(int limit = 100);
}

/// <summary>
/// RAG检索服务接口
/// </summary>
public interface IRagService
{
    /// <summary>
    /// 检索文档块
    /// </summary>
    RetrieveResponseDto Retrieve(RetrieveRequestDto request);

    /// <summary>
    /// RAG问答
    /// </summary>
    RagChatResponseDto Chat(RagChatRequestDto request);

    /// <summary>
    /// 获取对话历史列表
    /// </summary>
    IReadOnlyList<ConversationHistoryDto> GetConversationHistory(int offset, int limit, Guid? userId = null);

    /// <summary>
    /// 获取对话详情
    /// </summary>
    ConversationDetailDto? GetConversationDetail(Guid conversationId);

    /// <summary>
    /// 删除对话
    /// </summary>
    bool DeleteConversation(Guid conversationId);
}

/// <summary>
/// Dashboard服务接口
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// 获取Dashboard统计信息
    /// </summary>
    DashboardStatsDto GetStats();
}

/// <summary>
/// 图谱概览服务接口
/// </summary>
public interface IGraphOverviewService
{
    /// <summary>
    /// 获取图谱概览统计
    /// </summary>
    GraphOverviewDto GetOverview();
}

/// <summary>
/// 图谱概览DTO
/// </summary>
public record GraphOverviewDto(
    long TotalEntities,
    long TotalRelations,
    long DepartmentEntities,
    long PublicEntities,
    IReadOnlyDictionary<string, long> EntityTypeDistribution,
    IReadOnlyDictionary<string, long> RelationTypeDistribution);

/// <summary>
/// 部门服务接口
/// </summary>
public interface IDepartmentService
{
    IReadOnlyList<DepartmentDto> GetAll();
    DepartmentDto? GetById(Guid id);
    DepartmentDto Create(CreateDepartmentDto request, Guid userId);
    DepartmentDto? Update(Guid id, UpdateDepartmentDto request);
    bool Delete(Guid id);
    IReadOnlyList<UserListItemDto> GetMembers(Guid departmentId);
}

/// <summary>
/// 角色服务接口
/// </summary>
public interface IRoleService
{
    IReadOnlyList<RoleDto> GetAll();
    RoleDto? GetById(Guid id);
    RoleDto Create(CreateRoleDto request);
    RoleDto? Update(Guid id, UpdateRoleDto request);
    bool Delete(Guid id);
    RoleDto? UpdatePermissions(Guid id, PermissionMatrixDto permissions);
}

/// <summary>
/// 用户服务接口
/// </summary>
public interface IUserService
{
    UserPagedResultDto GetUsers(int page, int pageSize, Guid? departmentId = null, UserRole? role = null, bool? isActive = null, string? search = null);
    UserDto? GetById(Guid id);
    UserDto Create(CreateUserDto request, Guid createdBy);
    UserDto? Update(Guid id, UpdateUserDto request, Guid modifiedBy);
    bool Delete(Guid id);
    bool ResetPassword(Guid id, Guid modifiedBy);
    bool ChangePassword(Guid userId, ChangePasswordDto request);
    bool UpdateProfile(Guid userId, UpdateProfileDto request);
    bool ChangeStatus(Guid id, bool isActive, Guid modifiedBy);
    BatchOperationResultDto BatchOperation(BatchUserOperationDto request, Guid operatedBy);
}

/// <summary>
/// 用户分页结果
/// </summary>
public record UserPagedResultDto(
    IReadOnlyList<UserListItemDto> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// 批量用户操作类型
/// </summary>
public enum BatchUserOperationType
{
    Enable,
    Disable,
    Delete
}

/// <summary>
/// 批量用户操作请求
/// </summary>
public sealed record BatchUserOperationDto(
    BatchUserOperationType Operation,
    IReadOnlyList<Guid> UserIds);

/// <summary>
/// 批量操作结果
/// </summary>
public sealed record BatchOperationResultDto(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors);

/// <summary>
/// LLM提供商服务接口
/// </summary>
public interface ILlmProviderService
{
    IReadOnlyList<LlmProviderDto> GetAll();
    LlmProviderDto? GetById(Guid id);
    LlmProviderDto Create(CreateLlmProviderDto request);
    LlmProviderDto? Update(Guid id, CreateLlmProviderDto request);
    bool Delete(Guid id);
    IReadOnlyList<LlmModelDto> GetModelsByProviderId(Guid providerId);
    LlmModelDto? AddModel(CreateLlmModelDto request);
    LlmModelDto? UpdateModel(Guid modelId, UpdateLlmModelDto request);
    bool DeleteModel(Guid modelId);
    bool SetDefaultModel(Guid modelId);
    bool EnableModel(Guid modelId, bool enabled);
    (bool Success, string Message, double? ResponseTimeMs) TestConnection(Guid providerId);
    IReadOnlyList<LlmModelDto> GetBuiltInEmbeddingModels();
}

/// <summary>
/// 系统配置服务接口
/// </summary>
public interface ISystemConfigService
{
    SystemConfigDto GetConfig();
    SystemConfigDto UpdateConfig(UpdateSystemConfigDto request);
    SystemConfigDto ResetConfig();
}

/// <summary>
/// 连接器服务接口
/// </summary>
public interface IConnectorService
{
    IReadOnlyList<ConnectorDto> GetAll();
    ConnectorDto? GetById(Guid id);
    ConnectorDto Create(CreateConnectorDto request);
    ConnectorDto? Update(Guid id, CreateConnectorDto request);
    bool Delete(Guid id);
    ConnectorHealthResultDto CheckHealth(Guid id);
}

/// <summary>
/// 连接器健康检查结果
/// </summary>
public record ConnectorHealthResultDto(
    Guid ConnectorId,
    bool IsHealthy,
    string? Message,
    double? ResponseTimeMs);

/// <summary>
/// 审计日志服务接口
/// </summary>
public interface IAuditLogService
{
    AuditLogPagedResultDto GetLogs(int page, int pageSize, Guid? userId = null, string? action = null, DateTime? startTime = null, DateTime? endTime = null);
    AuditLogDto? GetById(Guid id);
    void Log(Guid userId, string action, string resourceType, Guid? resourceId, string? resourceName, string result, string? ipAddress, string? details);
}
