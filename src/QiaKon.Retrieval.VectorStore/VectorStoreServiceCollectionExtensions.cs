using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Retrieval.VectorStore;

/// <summary>
/// 向量存储 DI 注册扩展
/// </summary>
public static class VectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// 注册向量存储服务（需配合具体实现使用，如 NpgsqlVectorStore）
    /// </summary>
    public static IServiceCollection AddVectorStore(this IServiceCollection services)
    {
        // 核心抽象层不注册具体实现，仅提供扩展点
        // 具体实现（如 NpgsqlVectorStore）需在调用此方法后单独注册
        return services;
    }
}
