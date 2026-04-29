using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace QiaKon.Shared;

/// <summary>
/// Shared服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册Shared内存态业务服务
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        // HttpClient for connector health checks
        services.AddHttpClient("ConnectorHealthCheck")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3
            });

        // Core services
        services.AddSingleton<IAuthService, MemoryAuthService>();
        services.AddSingleton<IDocumentService, MemoryDocumentService>();
        services.AddSingleton<IGraphService, MemoryGraphService>();
        services.AddSingleton<IRagService, MemoryRagService>();

        // Dashboard & Overview
        services.AddSingleton<IDashboardService, MemoryDashboardService>();
        services.AddSingleton<IGraphOverviewService, MemoryGraphOverviewService>();

        // System Management
        services.AddSingleton<IDepartmentService, MemoryDepartmentService>();
        services.AddSingleton<IRoleService, MemoryRoleService>();
        services.AddSingleton<IUserService, MemoryUserService>();
        services.AddSingleton<ILlmProviderService, MemoryLlmProviderService>();
        services.AddSingleton<ISystemConfigService, MemorySystemConfigService>();
        services.AddSingleton<IConnectorService, MemoryConnectorService>();
        services.AddSingleton<IAuditLogService, MemoryAuditLogService>();

        return services;
    }
}
