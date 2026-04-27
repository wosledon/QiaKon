namespace QiaKon.EntityFrameworkCore;

/// <summary>
/// 审计上下文提供者接口，用于获取当前用户信息
/// </summary>
public interface IAuditContextProvider
{
    /// <summary>
    /// 获取当前操作用户的 ID
    /// </summary>
    Guid GetCurrentUserId();

    /// <summary>
    /// 获取当前操作用户的名称（可选）
    /// </summary>
    string? GetCurrentUserName();
}
