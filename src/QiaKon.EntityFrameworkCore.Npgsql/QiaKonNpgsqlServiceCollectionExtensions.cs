using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using QiaKon.EntityFrameworkCore;

namespace QiaKon.EntityFrameworkCore.Npgsql;

/// <summary>
/// PostgreSQL EF Core 服务注册扩展
/// </summary>
public static class QiaKonNpgsqlServiceCollectionExtensions
{
    /// <summary>
    /// 注册 PostgreSQL DbContext，包含优化配置和审计支持
    /// </summary>
    public static IServiceCollection AddQiaKonNpgsqlDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : QiaKonNpgsqlDbContext
    {
        // 注册审计上下文
        services.AddQiaKonAuditContext();

        // 注册 DbContext
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlBuilder =>
            {
                // 启用批量插入优化
                npgsqlBuilder.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);

                // 使用 EF Core Core 3.0+ 的批量操作
                npgsqlBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

                npgsqlOptionsAction?.Invoke(npgsqlBuilder);
            });

            // 启用敏感数据日志（开发环境）
#if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
#endif
        });

        return services;
    }

    /// <summary>
    /// 注册 PostgreSQL DbContext，使用自定义审计提供者
    /// </summary>
    public static IServiceCollection AddQiaKonNpgsqlDbContext<TContext, TAuditProvider>(
        this IServiceCollection services,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? npgsqlOptionsAction = null)
        where TContext : QiaKonNpgsqlDbContext
        where TAuditProvider : class, IAuditContextProvider
    {
        services.AddQiaKonAuditContext<TAuditProvider>();

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlBuilder =>
            {
                npgsqlBuilder.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);

                npgsqlBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

                npgsqlOptionsAction?.Invoke(npgsqlBuilder);
            });

#if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
#endif
        });

        return services;
    }

    /// <summary>
    /// 配置 PostgreSQL 连接池和性能选项
    /// </summary>
    public static NpgsqlDbContextOptionsBuilder WithQiaKonDefaults(
        this NpgsqlDbContextOptionsBuilder builder)
    {
        // 连接池优化
        builder.CommandTimeout(30);

        // 启用 EF Core 8+ 的批量操作
        builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

        return builder;
    }
}
