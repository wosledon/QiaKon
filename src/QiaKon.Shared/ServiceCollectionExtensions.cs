using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using QiaKon.EntityFrameworkCore.Npgsql;

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

    /// <summary>
    /// 注册 Shared 服务，并将核心业务数据落到 PostgreSQL
    /// </summary>
    public static IServiceCollection AddSharedServicesWithPostgres(this IServiceCollection services, string connectionString)
    {
        services.AddHttpClient("ConnectorHealthCheck")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3
            });

        services.AddQiaKonNpgsqlDbContext<QiaKonAppDbContext>(connectionString);
        services.AddScoped<QiaKonDatabaseInitializer>();

        // Core services backed by PostgreSQL
        services.AddScoped<IDocumentService, PostgresDocumentService>();
        services.AddScoped<IGraphService, PostgresGraphService>();
        services.AddScoped<IDashboardService, MemoryDashboardService>();
        services.AddScoped<IGraphOverviewService, MemoryGraphOverviewService>();

        // Keep low-risk auxiliary services in memory for now
        services.AddSingleton<IAuthService, MemoryAuthService>();
        services.AddSingleton<IRagService, MemoryRagService>();
        services.AddSingleton<IDepartmentService, MemoryDepartmentService>();
        services.AddSingleton<IRoleService, MemoryRoleService>();
        services.AddSingleton<IUserService, MemoryUserService>();
        services.AddSingleton<ILlmProviderService, MemoryLlmProviderService>();
        services.AddSingleton<ISystemConfigService, MemorySystemConfigService>();
        services.AddSingleton<IConnectorService, MemoryConnectorService>();
        services.AddSingleton<IAuditLogService, MemoryAuditLogService>();

        return services;
    }

    /// <summary>
    /// 初始化数据库结构和种子数据
    /// </summary>
    public static async Task InitializeQiaKonDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<QiaKonDatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
