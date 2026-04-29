using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// 获取Dashboard统计信息
    /// </summary>
    [HttpGet("stats")]
    public ApiResponse<DashboardStatsDto> GetStats()
    {
        var stats = _dashboardService.GetStats();
        return ApiResponse<DashboardStatsDto>.Ok(stats);
    }

    /// <summary>
    /// 获取最近上传的文档
    /// </summary>
    [HttpGet("recent-documents")]
    public ApiResponse<IReadOnlyList<RecentDocumentDto>> GetRecentDocuments([FromQuery] int limit = 5)
    {
        var stats = _dashboardService.GetStats();
        var documents = stats.RecentDocuments.Take(Math.Max(1, Math.Min(limit, 20))).ToList();
        return ApiResponse<IReadOnlyList<RecentDocumentDto>>.Ok(documents);
    }

    /// <summary>
    /// 获取最近的问答记录
    /// </summary>
    [HttpGet("recent-chats")]
    public ApiResponse<IReadOnlyList<RecentChatDto>> GetRecentChats([FromQuery] int limit = 5)
    {
        var stats = _dashboardService.GetStats();
        var chats = stats.RecentChats.Take(Math.Max(1, Math.Min(limit, 20))).ToList();
        return ApiResponse<IReadOnlyList<RecentChatDto>>.Ok(chats);
    }

    /// <summary>
    /// 获取系统组件健康状态
    /// </summary>
    [HttpGet("health")]
    public ApiResponse<IReadOnlyList<QiaKon.Contracts.DTOs.ComponentHealthDto>> GetHealth()
    {
        var stats = _dashboardService.GetStats();
        return ApiResponse<IReadOnlyList<QiaKon.Contracts.DTOs.ComponentHealthDto>>.Ok(stats.ComponentHealth);
    }
}
