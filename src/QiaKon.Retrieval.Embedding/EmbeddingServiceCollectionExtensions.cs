using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Retrieval.Embedding;

/// <summary>
/// 嵌入服务注册扩展
/// </summary>
public static class EmbeddingServiceCollectionExtensions
{
    /// <summary>
    /// 注册本地嵌入服务
    /// </summary>
    public static IServiceCollection AddLocalEmbedding(
        this IServiceCollection services,
        Action<EmbeddingOptions> configureOptions)
    {
        var options = new EmbeddingOptions();
        configureOptions(options);

        services.AddSingleton(options);
        services.AddSingleton<IEmbeddingService>(sp =>
            new LocalEmbeddingService(
                options,
                sp.GetService<Microsoft.Extensions.Logging.ILogger<LocalEmbeddingService>>()));

        return services;
    }

    /// <summary>
    /// 注册本地嵌入服务（使用预配置选项）
    /// </summary>
    public static IServiceCollection AddLocalEmbedding(
        this IServiceCollection services,
        EmbeddingOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IEmbeddingService>(sp =>
            new LocalEmbeddingService(
                options,
                sp.GetService<Microsoft.Extensions.Logging.ILogger<LocalEmbeddingService>>()));

        return services;
    }
}
