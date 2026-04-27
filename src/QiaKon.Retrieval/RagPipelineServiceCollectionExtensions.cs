using Microsoft.Extensions.DependencyInjection;
using QiaKon.Retrieval.Embedding;
using QiaKon.Retrieval.VectorStore;

namespace QiaKon.Retrieval;

/// <summary>
/// RAG 管道服务注册扩展
/// </summary>
public static class RagPipelineServiceCollectionExtensions
{
    /// <summary>
    /// 注册 RAG 管道
    /// </summary>
    public static IServiceCollection AddRagPipeline(this IServiceCollection services)
    {
        services.AddSingleton<IRagPipeline, RagPipeline>();
        return services;
    }

    /// <summary>
    /// 注册完整的 RAG 基础设施（需配合具体实现使用）
    /// </summary>
    public static IServiceCollection AddRagInfrastructure(this IServiceCollection services)
    {
        // 注册核心抽象
        services.AddRagPipeline();

        // 注意：以下具体实现需要在调用此方法后单独注册：
        // - IChunkingStrategy（通过 AddCharacterChunking / AddParagraphChunking / AddMoEChunking 等）
        // - IEmbeddingService（由具体 Provider 实现注册）
        // - IVectorStore（通过 AddNpgsqlVectorStore 等）

        return services;
    }
}
