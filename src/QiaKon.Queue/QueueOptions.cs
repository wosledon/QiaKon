namespace QiaKon.Queue;

/// <summary>
/// 队列配置选项基类
/// </summary>
public abstract class QueueOptions
{
    /// <summary>
    /// 队列类型
    /// </summary>
    public abstract QueueType Type { get; }

    /// <summary>
    /// 连接字符串/服务器地址
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 默认主题
    /// </summary>
    public string? DefaultTopic { get; set; }

    /// <summary>
    /// 默认分区数（-1 表示使用队列实现的默认值）
    /// </summary>
    public int DefaultPartitionCount { get; set; } = -1;
}

/// <summary>
/// 内存队列配置选项
/// </summary>
public sealed class MemoryQueueOptions : QueueOptions
{
    public override QueueType Type => QueueType.Memory;

    /// <summary>
    /// 每个主题的默认分区数（默认 4）
    /// </summary>
    public int PartitionCount { get; set; } = 4;

    /// <summary>
    /// 通道容量（默认 10000）
    /// </summary>
    public int ChannelCapacity { get; set; } = 10000;

    /// <summary>
    /// 是否允许多消费者（单消费者模式使用 BoundedChannel，否则使用 SPSC）
    /// </summary>
    public bool AllowMultipleConsumers { get; set; } = false;

    /// <summary>
    /// 消费超时（默认 1 秒）
    /// </summary>
    public TimeSpan ConsumeTimeout { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Kafka 队列配置选项
/// </summary>
public sealed class KafkaQueueOptions : QueueOptions
{
    public override QueueType Type => QueueType.Kafka;

    /// <summary>
    /// Bootstrap servers (例如: localhost:9092)
    /// </summary>
    public required string BootstrapServers { get; set; }

    /// <summary>
    /// 消费者组 ID 前缀
    /// </summary>
    public string? GroupIdPrefix { get; set; }

    /// <summary>
    /// 自动提交间隔
    /// </summary>
    public TimeSpan AutoCommitInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 消费者会话超时
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 最大.poll间隔
    /// </summary>
    public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 消息确认级别
    /// </summary>
    public KafkaAcks Acks { get; set; } = KafkaAcks.Leader;

    /// <summary>
    /// 生产者 linger 时间
    /// </summary>
    public TimeSpan Linger { get; set; } = TimeSpan.FromMilliseconds(5);

    /// <summary>
    /// 批量大小（字节）
    /// </summary>
    public int BatchSize { get; set; } = 16384;
}

/// <summary>
/// Kafka 消息确认级别
/// </summary>
public enum KafkaAcks
{
    /// <summary>
    /// 无需确认
    /// </summary>
    None,

    /// <summary>
    /// Leader 确认
    /// </summary>
    Leader,

    /// <summary>
    /// 所有副本确认
    /// </summary>
    All
}
