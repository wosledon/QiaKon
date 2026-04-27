namespace QiaKon.Queue.Memory;

/// <summary>
/// 内存队列生产者
/// </summary>
public sealed class MemoryQueueProducer : IQueueProducer
{
    private readonly MemoryQueue _queue;
    private bool _disposed;

    public string Name { get; }

    public MemoryQueueProducer(string name, MemoryQueue queue)
    {
        Name = name;
        _queue = queue;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task ProduceAsync(
        string topic,
        ReadOnlyMemory<byte> message,
        string? key = null,
        int partition = -1,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MemoryQueueProducer));
        if (string.IsNullOrEmpty(topic)) throw new ArgumentNullException(nameof(topic));

        await _queue.ProduceAsync(topic, message, key, partition, headers, cancellationToken);
    }

    public async Task ProduceManyAsync(
        string topic,
        IEnumerable<ReadOnlyMemory<byte>> messages,
        int partition = -1,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MemoryQueueProducer));
        if (string.IsNullOrEmpty(topic)) throw new ArgumentNullException(nameof(topic));
        if (messages == null) throw new ArgumentNullException(nameof(messages));

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _queue.ProduceAsync(topic, message, null, partition, null, cancellationToken);
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // Channel 无需 flush
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.UnregisterProducer(Name);
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
