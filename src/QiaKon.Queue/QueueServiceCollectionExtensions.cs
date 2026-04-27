using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Queue;

public static class QueueServiceCollectionExtensions
{
    /// <summary>
    /// 添加队列（根据配置类型自动选择）
    /// </summary>
    public static IServiceCollection AddQueue(
        this IServiceCollection services,
        QueueOptions options)
    {
        services.AddSingleton(options);
        return services;
    }
}
