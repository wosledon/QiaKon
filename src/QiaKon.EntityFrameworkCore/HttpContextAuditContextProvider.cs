using Microsoft.AspNetCore.Http;

namespace QiaKon.EntityFrameworkCore;

/// <summary>
/// 基于 HttpContext 的审计上下文提供者实现
/// </summary>
public class HttpContextAuditContextProvider : IAuditContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuditContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("UserId")
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("id");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return Guid.Empty;
    }

    public string? GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("name")?.Value;
    }
}
