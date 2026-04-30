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
        services.AddScoped<DocumentIndexingRuntime>();
        services.AddScoped<IDashboardService, PostgresDashboardService>();
        services.AddScoped<IGraphOverviewService, PostgresGraphOverviewService>();
        services.AddScoped<IAuthService, PostgresAuthService>();
        services.AddScoped<ConfiguredLlmModelResolver>();
        services.AddScoped<IRagService, PostgresRagService>();
        services.AddScoped<IDepartmentService, PostgresDepartmentService>();
        services.AddScoped<IRoleService, PostgresRoleService>();
        services.AddScoped<IUserService, PostgresUserService>();
        services.AddScoped<ILlmProviderService, PostgresLlmProviderService>();
        services.AddScoped<ISystemConfigService, PostgresSystemConfigService>();
        services.AddScoped<IConnectorService, PostgresConnectorService>();
        services.AddScoped<IAuditLogService, PostgresAuditLogService>();

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
