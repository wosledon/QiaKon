using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.EntityFrameworkCore;

/// <summary>
/// EF Core 服务注册扩展
/// </summary>
public static class QiaKonEntityFrameworkServiceCollectionExtensions
{
    /// <summary>
    /// 注册审计上下文提供者（基于 HttpContext）
    /// </summary>
    public static IServiceCollection AddQiaKonAuditContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditContextProvider, HttpContextAuditContextProvider>();
        return services;
    }

    /// <summary>
    /// 注册自定义的审计上下文提供者
    /// </summary>
    public static IServiceCollection AddQiaKonAuditContext<TProvider>(this IServiceCollection services)
        where TProvider : class, IAuditContextProvider
    {
        services.AddScoped<IAuditContextProvider, TProvider>();
        return services;
    }
}
