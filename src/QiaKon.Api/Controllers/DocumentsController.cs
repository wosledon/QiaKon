using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IDocumentService documentService, ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// 获取文档列表
    /// </summary>
    [HttpGet]
    public ApiResponse<DocumentPagedResultDto> GetDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] IndexStatus? status = null)
    {
        var result = _documentService.GetDocuments(page, pageSize, departmentId, status);
        return ApiResponse<DocumentPagedResultDto>.Ok(result);
    }

    /// <summary>
    /// 获取文档详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public ApiResponse<DocumentDetailDto> GetById(Guid id)
    {
        var doc = _documentService.GetDocument(id);
        return doc is null
            ? ApiResponse<DocumentDetailDto>.Fail("文档不存在", 404)
            : ApiResponse<DocumentDetailDto>.Ok(doc);
    }

    /// <summary>
    /// 创建文档
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<DocumentDetailDto>> Create([FromForm] IFormFile file, [FromForm] UploadDocumentFormDto form, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ApiResponse<DocumentDetailDto>.Fail("请上传有效文件", 400);
        }

        var userId = GetCurrentUserId();
        var doc = await _documentService.UploadDocumentAsync(file, form, userId, cancellationToken);
        _logger.LogInformation("Document created: {Id} by user {UserId}", doc.Id, userId);
        return ApiResponse<DocumentDetailDto>.Ok(doc, "文档上传成功");
    }

    /// <summary>
    /// 更新文档
    /// </summary>
    [HttpPut("{id:guid}")]
    public ApiResponse<DocumentDetailDto> Update(Guid id, [FromBody] UpdateDocumentRequestDto request)
    {
        var userId = GetCurrentUserId();
        var doc = _documentService.UpdateDocument(id, request, userId);

        if (doc is null)
            return ApiResponse<DocumentDetailDto>.Fail("文档不存在", 404);

        _logger.LogInformation("Document updated: {Id} by user {UserId}", id, userId);
        return ApiResponse<DocumentDetailDto>.Ok(doc, "文档更新成功");
    }

    /// <summary>
    /// 删除文档
    /// </summary>
    [HttpDelete("{id:guid}")]
    public ApiResponse Delete(Guid id)
    {
        var result = _documentService.DeleteDocument(id);

        if (!result)
            return ApiResponse.Fail("文档不存在", 404);

        _logger.LogInformation("Document deleted: {Id}", id);
        return ApiResponse.Ok("文档删除成功");
    }

    /// <summary>
    /// 重建文档索引
    /// </summary>
    [HttpPost("reindex")]
    public ApiResponse<ReindexResponseDto> Reindex([FromBody] ReindexRequestDto? request)
    {
        var result = _documentService.Reindex(request?.DocumentId);
        _logger.LogInformation("Reindex completed: {Message}", result.Message);
        return ApiResponse<ReindexResponseDto>.Ok(result);
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
