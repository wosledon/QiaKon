using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Retrieval.DocumentProcessor;

/// <summary>
/// 文档处理器服务注册扩展
/// </summary>
public static class DocumentProcessorServiceCollectionExtensions
{
    /// <summary>
    /// 注册 MarkItDown 文档处理器
    /// </summary>
    public static IServiceCollection AddMarkItDownDocumentProcessor(
        this IServiceCollection services,
        Action<DocumentProcessorOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<DocumentProcessorOptions>();
        }

        services.AddSingleton<IDocumentProcessor, MarkItDownDocumentProcessor>();
        return services;
    }

    /// <summary>
    /// 注册 MarkItDown 文档处理器（使用显式选项）
    /// </summary>
    public static IServiceCollection AddMarkItDownDocumentProcessor(
        this IServiceCollection services,
        DocumentProcessorOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IDocumentProcessor, MarkItDownDocumentProcessor>();
        return services;
    }

    /// <summary>
    /// 注册自定义文档处理器
    /// </summary>
    public static IServiceCollection AddDocumentProcessor<TProcessor>(
        this IServiceCollection services)
        where TProcessor : class, IDocumentProcessor
    {
        services.AddSingleton<IDocumentProcessor, TProcessor>();
        return services;
    }
}
