namespace QiaKon.Queue;

/// <summary>
/// 队列异常
/// </summary>
public class QueueException : Exception
{
    public string? QueueName { get; }
    public string? Topic { get; }

    public QueueException(string message) : base(message) { }

    public QueueException(string message, Exception innerException) : base(message, innerException) { }

    public QueueException(string message, string? queueName, string? topic) : base(message)
    {
        QueueName = queueName;
        Topic = topic;
    }

    public QueueException(string message, string? queueName, string? topic, Exception innerException) : base(message, innerException)
    {
        QueueName = queueName;
        Topic = topic;
    }
}

/// <summary>
/// 队列消费异常
/// </summary>
public class QueueConsumeException : QueueException
{
    public IQueueMessage? FailedMessage { get; }

    public QueueConsumeException(string message, IQueueMessage? failedMessage = null)
        : base(message)
    {
        FailedMessage = failedMessage;
    }

    public QueueConsumeException(string message, Exception innerException, IQueueMessage? failedMessage = null)
        : base(message, innerException)
    {
        FailedMessage = failedMessage;
    }
}

/// <summary>
/// 队列生产异常
/// </summary>
public class QueueProduceException : QueueException
{
    public ReadOnlyMemory<byte>? FailedMessage { get; }

    public QueueProduceException(string message, ReadOnlyMemory<byte>? failedMessage = null)
        : base(message)
    {
        FailedMessage = failedMessage;
    }

    public QueueProduceException(string message, Exception innerException, ReadOnlyMemory<byte>? failedMessage = null)
        : base(message, innerException)
    {
        FailedMessage = failedMessage;
    }
}
