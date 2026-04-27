using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Queue.Memory;

public static class MemoryQueueExtensions
{
    /// <summary>
    /// 添加内存队列
    /// </summary>
    public static IServiceCollection AddMemoryQueue(
        this IServiceCollection services,
        Action<MemoryQueueOptions>? configure = null)
    {
        var options = new MemoryQueueOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<IQueue, MemoryQueue>();
        return services;
    }
}
