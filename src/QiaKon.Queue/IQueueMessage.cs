namespace QiaKon.Queue;

/// <summary>
/// 队列消息接口
/// </summary>
public interface IQueueMessage
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 主题/Topic
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// 分区号（-1 表示未分配）
    /// </summary>
    int Partition { get; }

    /// <summary>
    /// 消息体
    /// </summary>
    ReadOnlyMemory<byte> Body { get; }

    /// <summary>
    /// 时间戳
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// 消息头
    /// </summary>
    IReadOnlyDictionary<string, string> Headers { get; }
}

/// <summary>
/// 队列消息基类
/// </summary>
public abstract class QueueMessageBase : IQueueMessage
{
    public required string Id { get; init; }
    public required string Topic { get; init; }
    public int Partition { get; init; } = -1;
    public required ReadOnlyMemory<byte> Body { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;

    private static readonly Dictionary<string, string> EmptyHeaders = new();

    public static QueueMessageBase Create(string topic, ReadOnlyMemory<byte> body, string? id = null, Dictionary<string, string>? headers = null, int partition = -1)
    {
        return new GenericQueueMessage
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            Topic = topic,
            Partition = partition,
            Body = body,
            Headers = headers ?? EmptyHeaders
        };
    }
}

/// <summary>
/// 通用队列消息实现
/// </summary>
public sealed class GenericQueueMessage : QueueMessageBase
{
}
