using System.Runtime.CompilerServices;

namespace QiaKon.Queue.Kafka;

/// <summary>
/// Kafka 队列消费者
/// </summary>
public sealed class KafkaQueueConsumer : IQueueConsumer
{
    private readonly KafkaConsumer _consumer;
    private bool _disposed;
    private bool _paused;

    public string Name { get; }
    public string GroupId { get; }
    public IReadOnlyList<string> Topics { get; }

    public KafkaQueueConsumer(string name, KafkaConsumer consumer)
    {
        Name = name;
        GroupId = consumer.Topics.FirstOrDefault() ?? string.Empty;
        Topics = consumer.Topics;
        _consumer = consumer;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<IQueueMessage> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(KafkaQueueConsumer));

        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            if (_paused)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            var result = _consumer.Consume(cancellationToken);
            if (result != null)
            {
                yield return KafkaQueueMessage.FromConsumeResult(result);
            }
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        _consumer.Commit();
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        _paused = true;
        _consumer.Pause();
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        _paused = false;
        _consumer.Resume();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
