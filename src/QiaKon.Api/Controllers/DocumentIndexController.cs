using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

/// <summary>
/// 文档索引管理控制器
/// </summary>
[ApiController]
[Route("api/documents/index")]
public class DocumentIndexController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentIndexController> _logger;

    public DocumentIndexController(IDocumentService documentService, ILogger<DocumentIndexController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// 获取索引队列状态（待索引/索引中/已完成的文档列表及进度）
    /// </summary>
    [HttpGet]
    public ApiResponse<IndexQueueStatusDto> GetQueueStatus()
    {
        var status = _documentService.GetIndexQueueStatus();
        return ApiResponse<IndexQueueStatusDto>.Ok(status);
    }

    /// <summary>
    /// 批量重试失败任务
    /// </summary>
    [HttpPost("retry")]
    public ApiResponse<ReindexResponseDto> RetryFailed()
    {
        var result = _documentService.RetryFailedIndexing();
        _logger.LogInformation("Retry failed indexing tasks: {Message}", result.Message);
        return ApiResponse<ReindexResponseDto>.Ok(result, result.Message);
    }

    /// <summary>
    /// 全量重建索引
    /// </summary>
    [HttpPost("rebuild")]
    public ApiResponse<ReindexResponseDto> RebuildAll()
    {
        var result = _documentService.Reindex(null);
        _logger.LogInformation("Rebuild all indexes: {Message}", result.Message);
        return ApiResponse<ReindexResponseDto>.Ok(result, result.Message);
    }

    /// <summary>
    /// 获取索引统计（总块数、成功率、平均耗时）
    /// </summary>
    [HttpGet("stats")]
    public ApiResponse<IndexStatsDto> GetStats()
    {
        var stats = _documentService.GetIndexStats();
        return ApiResponse<IndexStatsDto>.Ok(stats);
    }
}
