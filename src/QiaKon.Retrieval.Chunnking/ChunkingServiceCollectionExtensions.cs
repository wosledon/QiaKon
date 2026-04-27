using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Retrieval.Chunnking;

/// <summary>
/// 分块策略服务注册扩展
/// </summary>
public static class ChunkingServiceCollectionExtensions
{
    /// <summary>
    /// 注册字符分块策略
    /// </summary>
    public static IServiceCollection AddCharacterChunking(
        this IServiceCollection services,
        Action<CharacterChunkingOptions>? configureOptions = null)
    {
        var options = new CharacterChunkingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton<IChunkingStrategy>(new CharacterChunkingStrategy(options));
        return services;
    }

    /// <summary>
    /// 注册段落分块策略
    /// </summary>
    public static IServiceCollection AddParagraphChunking(
        this IServiceCollection services,
        Action<ParagraphChunkingOptions>? configureOptions = null)
    {
        var options = new ParagraphChunkingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton<IChunkingStrategy>(new ParagraphChunkingStrategy(options));
        return services;
    }

    /// <summary>
    /// 注册自定义分块策略
    /// </summary>
    public static IServiceCollection AddChunkingStrategy<TStrategy>(
        this IServiceCollection services)
        where TStrategy : class, IChunkingStrategy
    {
        services.AddSingleton<IChunkingStrategy, TStrategy>();
        return services;
    }
}
