using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QiaKon.Graph.Engine;

namespace QiaKon.Graph.Engine.Npgsql;

/// <summary>
/// 图引擎 DI 注册扩展
/// </summary>
public static class GraphEngineServiceCollectionExtensions
{
    /// <summary>
    /// 注册 PostgreSQL 图引擎
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="options">图引擎配置</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddNpgsqlGraphEngine(
        this IServiceCollection services,
        GraphEngineOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IGraphEngine>(sp =>
        {
            if (options.LoggerFactory == null)
            {
                options.LoggerFactory = sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            }
            return new NpgsqlGraphEngine(options);
        });
        return services;
    }

    /// <summary>
    /// 注册 PostgreSQL 图引擎（使用配置委托）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddNpgsqlGraphEngine(
        this IServiceCollection services,
        Action<GraphEngineOptions> configureOptions)
    {
        var options = new GraphEngineOptions
        {
            ConnectionString = string.Empty // required 属性提供一个默认值
        };
        configureOptions(options);

        return services.AddNpgsqlGraphEngine(options);
    }
}
