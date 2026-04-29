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

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".md", ".txt"
    };

    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

    public DocumentsController(IDocumentService documentService, ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// 获取文档列表（支持搜索和排序）
    /// </summary>
    [HttpGet]
    public ApiResponse<DocumentPagedResultDto> GetDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] IndexStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] bool sortDescending = true)
    {
        var result = _documentService.GetDocuments(page, pageSize, departmentId, status, search, sortBy, sortDescending);
        return ApiResponse<DocumentPagedResultDto>.Ok(result);
    }

    /// <summary>
    /// 获取文档详情（包含分块信息）
    /// </summary>
    [HttpGet("{id:guid}")]
    public ApiResponse<DocumentDetailWithChunksDto> GetById(Guid id)
    {
        var doc = _documentService.GetDocumentWithChunks(id);
        return doc is null
            ? ApiResponse<DocumentDetailWithChunksDto>.Fail("文档不存在", 404)
            : ApiResponse<DocumentDetailWithChunksDto>.Ok(doc);
    }

    /// <summary>
    /// 上传文档（multipart/form-data）
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<ApiResponse<DocumentDetailDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] UploadDocumentFormDto form,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return ApiResponse<DocumentDetailDto>.Fail("请上传有效文件", 400);

        if (file.Length > MaxFileSize)
            return ApiResponse<DocumentDetailDto>.Fail("文件大小不能超过50MB", 400);

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return ApiResponse<DocumentDetailDto>.Fail("不支持的文件类型，仅支持PDF/Word/Markdown/TXT", 400);

        var userId = GetCurrentUserId();
        var doc = await _documentService.UploadDocumentAsync(file, form, userId, cancellationToken);
        _logger.LogInformation("Document uploaded: {Id} by user {UserId}, file: {FileName}", doc.Id, userId, file.FileName);
        return ApiResponse<DocumentDetailDto>.Ok(doc, "文档上传成功");
    }

    /// <summary>
    /// 更新文档元数据
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
    /// 批量删除文档
    /// </summary>
    [HttpPost("batch")]
    public ApiResponse<BatchDeleteResponseDto> BatchDelete([FromBody] BatchDeleteRequestDto request)
    {
        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
            return ApiResponse<BatchDeleteResponseDto>.Fail("请提供要删除的文档ID列表", 400);

        var result = _documentService.BatchDeleteDocuments(request.DocumentIds);
        _logger.LogInformation("Batch deleted {DeletedCount} documents, {FailedCount} failed", result.DeletedCount, result.FailedCount);
        return ApiResponse<BatchDeleteResponseDto>.Ok(result, $"成功删除 {result.DeletedCount} 个文档");
    }

    /// <summary>
    /// 下载原文件
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public ApiResponse<object> Download(Guid id)
    {
        var info = _documentService.GetDocumentDownloadInfo(id);
        if (info is null)
            return ApiResponse<object>.Fail("文档不存在", 404);

        // 返回下载信息，实际文件流由静态文件中间件或其他方式处理
        return ApiResponse<object>.Ok(new
        {
            FilePath = info.Value.FilePath,
            FileName = info.Value.FileName
        }, "获取下载信息成功");
    }

    /// <summary>
    /// 重新索引单个文档
    /// </summary>
    [HttpPost("{id:guid}/reindex")]
    public ApiResponse<ReindexResponseDto> ReindexSingle(Guid id)
    {
        if (_documentService.GetDocument(id) is null)
            return ApiResponse<ReindexResponseDto>.Fail("文档不存在", 404);

        var result = _documentService.Reindex(id);
        _logger.LogInformation("Reindex single document: {Id}, {Message}", id, result.Message);
        return ApiResponse<ReindexResponseDto>.Ok(result);
    }

    /// <summary>
    /// 重新解析文档
    /// </summary>
    [HttpPost("{id:guid}/reparse")]
    public ApiResponse<ReparseResponseDto> Reparse(Guid id, [FromBody] ReparseRequestDto? request)
    {
        var result = _documentService.ReparseDocument(id, request?.ChunkingStrategy);
        if (result.Message == "文档不存在")
            return ApiResponse<ReparseResponseDto>.Fail("文档不存在", 404);

        _logger.LogInformation("Reparse document: {Id}, {Message}", id, result.Message);
        return ApiResponse<ReparseResponseDto>.Ok(result);
    }

    /// <summary>
    /// 获取索引队列状态
    /// </summary>
    [HttpGet("index/queue")]
    public ApiResponse<IndexQueueStatusDto> GetIndexQueue()
    {
        var status = _documentService.GetIndexQueueStatus();
        return ApiResponse<IndexQueueStatusDto>.Ok(status);
    }

    /// <summary>
    /// 获取索引统计
    /// </summary>
    [HttpGet("index/stats")]
    public ApiResponse<IndexStatsDto> GetIndexStats()
    {
        var stats = _documentService.GetIndexStats();
        return ApiResponse<IndexStatsDto>.Ok(stats);
    }

    /// <summary>
    /// 重试失败的索引任务
    /// </summary>
    [HttpPost("index/retry-failed")]
    public ApiResponse<ReindexResponseDto> RetryFailed()
    {
        var result = _documentService.RetryFailedIndexing();
        return ApiResponse<ReindexResponseDto>.Ok(result);
    }

    /// <summary>
    /// 重建所有文档索引
    /// </summary>
    [HttpPost("index/rebuild")]
    public ApiResponse<ReindexResponseDto> RebuildIndex()
    {
        var result = _documentService.Reindex(null);
        return ApiResponse<ReindexResponseDto>.Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
