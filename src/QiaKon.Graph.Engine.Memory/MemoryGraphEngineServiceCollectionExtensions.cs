using Microsoft.Extensions.DependencyInjection;
using QiaKon.Graph.Engine;

namespace QiaKon.Graph.Engine.Memory;

/// <summary>
/// Memory 图引擎 DI 注册扩展
/// </summary>
public static class MemoryGraphEngineServiceCollectionExtensions
{
    /// <summary>
    /// 注册内存图引擎（用于测试或轻量级场景）
    /// </summary>
    public static IServiceCollection AddMemoryGraphEngine(this IServiceCollection services)
    {
        services.AddSingleton<IGraphEngine, MemoryGraphEngine>();
        return services;
    }
}
