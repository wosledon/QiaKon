using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IAuthService, MemoryAuthService>();
        services.AddSingleton<IDocumentService, MemoryDocumentService>();
        services.AddSingleton<IGraphService, MemoryGraphService>();
        services.AddSingleton<IRagService, MemoryRagService>();

        return services;
    }
}
