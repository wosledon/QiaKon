using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/admin/audit")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AdminAuditController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// 获取审计日志列表
    /// </summary>
    [HttpGet("logs")]
    public ApiResponse<AuditLogPagedResultDto> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        var result = _auditLogService.GetLogs(page, pageSize, userId, action, startTime, endTime);
        return ApiResponse<AuditLogPagedResultDto>.Ok(result);
    }

    /// <summary>
    /// 获取审计日志详情
    /// </summary>
    [HttpGet("logs/{id:guid}")]
    public ApiResponse<AuditLogDto> GetById(Guid id)
    {
        var log = _auditLogService.GetById(id);
        return log is null
            ? ApiResponse<AuditLogDto>.Fail("日志不存在", 404)
            : ApiResponse<AuditLogDto>.Ok(log);
    }
}
