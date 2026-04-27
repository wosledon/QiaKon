namespace QiaKon.Queue;

/// <summary>
/// 队列接口 - 工厂接口
/// </summary>
public interface IQueue : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 队列名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 创建生产者
    /// </summary>
    /// <param name="name">生产者名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生产者实例</returns>
    Task<IQueueProducer> CreateProducerAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建消费者
    /// </summary>
    /// <param name="name">消费者名称</param>
    /// <param name="groupId">消费者组ID</param>
    /// <param name="topics">订阅主题列表</param>
    /// <param name="partitions">订阅分区（null 表示订阅所有分区）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消费者实例</returns>
    Task<IQueueConsumer> CreateConsumerAsync(
        string name,
        string groupId,
        string[] topics,
        int[]? partitions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 初始化队列
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取队列类型
    /// </summary>
    QueueType Type { get; }

    /// <summary>
    /// 获取主题的分区数（-1 表示不支持）
    /// </summary>
    Task<int> GetPartitionCountAsync(string topic, CancellationToken cancellationToken = default);
}

/// <summary>
/// 队列类型
/// </summary>
public enum QueueType
{
    /// <summary>
    /// 内存队列（Channel）
    /// </summary>
    Memory,

    /// <summary>
    /// Kafka 消息队列
    /// </summary>
    Kafka
}
