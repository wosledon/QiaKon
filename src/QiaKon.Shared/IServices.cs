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
    /// 获取文档列表
    /// </summary>
    DocumentPagedResultDto GetDocuments(int page, int pageSize, Guid? departmentId = null, IndexStatus? status = null);

    /// <summary>
    /// 获取文档详情
    /// </summary>
    DocumentDetailDto? GetDocument(Guid id);

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
    /// 重建文档索引
    /// </summary>
    ReindexResponseDto Reindex(Guid? documentId);
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
    /// 创建关系
    /// </summary>
    GraphRelationDto CreateRelation(CreateRelationRequestDto request, Guid userId);

    /// <summary>
    /// 图谱查询
    /// </summary>
    GraphQueryResponseDto Query(GraphQueryRequestDto request);
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
}
