using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Retrieval.VectorStore.Npgsql;

/// <summary>
/// PostgreSQL 向量存储 DI 注册扩展
/// </summary>
public static class NpgsqlVectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// 注册 PostgreSQL 向量存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="options">配置选项</param>
    public static IServiceCollection AddNpgsqlVectorStore(
        this IServiceCollection services,
        NpgsqlVectorStoreOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IVectorStore>(provider =>
        {
            var store = new NpgsqlVectorStore(options);
            store.InitializeAsync().GetAwaiter().GetResult();
            return store;
        });

        return services;
    }

    /// <summary>
    /// 注册 PostgreSQL 向量存储（使用配置工厂）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    public static IServiceCollection AddNpgsqlVectorStore(
        this IServiceCollection services,
        Action<NpgsqlVectorStoreOptions> configureOptions)
    {
        var options = new NpgsqlVectorStoreOptions();
        configureOptions(options);
        return services.AddNpgsqlVectorStore(options);
    }
}
