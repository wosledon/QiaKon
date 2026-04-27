using Microsoft.Extensions.DependencyInjection;

namespace QiaKon.Queue.Kafka;

public static class KafkaQueueExtensions
{
    /// <summary>
    /// 添加 Kafka 队列
    /// </summary>
    public static IServiceCollection AddKafkaQueue(
        this IServiceCollection services,
        Action<KafkaQueueOptions> configure)
    {
        var options = new KafkaQueueOptions { BootstrapServers = string.Empty };
        configure(options);
        services.AddSingleton(options);
        services.AddSingleton<IQueue, KafkaQueue>();
        return services;
    }
}
