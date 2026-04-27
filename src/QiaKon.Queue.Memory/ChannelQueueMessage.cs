using System.Threading.Channels;

namespace QiaKon.Queue.Memory;

/// <summary>
/// Channel 队列消息实现
/// </summary>
internal sealed class ChannelQueueMessage : IQueueMessage
{
    private static readonly Dictionary<string, string> EmptyHeaders = new();

    public required string Id { get; init; }
    public required string Topic { get; init; }
    public int Partition { get; init; } = -1;
    public required ReadOnlyMemory<byte> Body { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;

    public static ChannelQueueMessage Create(string topic, ReadOnlyMemory<byte> body, string? key = null, Dictionary<string, string>? headers = null, int partition = -1)
    {
        var headerDict = headers != null ? new Dictionary<string, string>(headers) : new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(key))
        {
            headerDict["_key"] = key;
        }

        return new ChannelQueueMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Topic = topic,
            Partition = partition,
            Body = body,
            Headers = headerDict,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Channel wrapper for multi-consumer support
/// </summary>
internal sealed class ChannelWrapper
{
    private readonly Channel<ChannelQueueMessage> _channel;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly HashSet<string> _consumerIds = new();
    private long _totalProduced;
    private long _totalConsumed;

    public ChannelWrapper(Channel<ChannelQueueMessage> channel)
    {
        _channel = channel;
    }

    public long TotalProduced => Interlocked.Read(ref _totalProduced);
    public long TotalConsumed => Interlocked.Read(ref _totalConsumed);

    public ValueTask WriteAsync(ChannelQueueMessage message)
    {
        Interlocked.Increment(ref _totalProduced);
        return _channel.Writer.WriteAsync(message);
    }

    public async ValueTask<ChannelQueueMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var message = await _channel.Reader.ReadAsync(cancellationToken);
            Interlocked.Increment(ref _totalConsumed);
            return message;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public bool TryPeek(out ChannelQueueMessage? message)
    {
        return _channel.Reader.TryPeek(out message);
    }

    public int Count => _channel.Reader.Count;

    public IReadOnlyList<string> ConsumerIds
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _consumerIds.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void RegisterConsumer(string consumerId)
    {
        _lock.EnterWriteLock();
        try
        {
            _consumerIds.Add(consumerId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void UnregisterConsumer(string consumerId)
    {
        _lock.EnterWriteLock();
        try
        {
            _consumerIds.Remove(consumerId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool HasConsumers
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _consumerIds.Count > 0;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public ChannelReader<ChannelQueueMessage> Reader => _channel.Reader;
    public bool Completion => _channel.Reader.Completion.IsCompleted;

    public void CompleteWriter()
    {
        _channel.Writer.Complete();
    }
}
