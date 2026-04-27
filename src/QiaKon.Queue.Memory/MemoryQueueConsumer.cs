using System.Runtime.CompilerServices;

namespace QiaKon.Queue.Memory;

/// <summary>
/// 内存队列消费者（支持按分区消费）
/// </summary>
public sealed class MemoryQueueConsumer : IQueueConsumer
{
    private readonly MemoryQueue _queue;
    // topic -> partitions
    private readonly Dictionary<string, int[]> _subscriptions;
    private bool _disposed;
    private bool _paused;
    private int _currentTopicIndex;
    private int _currentPartitionIndex;

    public string Name { get; }
    public string GroupId { get; }
    public IReadOnlyList<string> Topics => _subscriptions.Keys.ToList();

    public MemoryQueueConsumer(string name, string groupId, Dictionary<string, int[]> subscriptions, MemoryQueue queue)
    {
        Name = name;
        GroupId = groupId;
        _subscriptions = subscriptions;
        _queue = queue;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<IQueueMessage> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MemoryQueueConsumer));
        if (_subscriptions.Count == 0) yield break;

        var topics = _subscriptions.Keys.ToArray();

        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            if (_paused)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            // 轮询所有订阅的主题和分区
            for (var topicIdx = 0; topicIdx < topics.Length && !cancellationToken.IsCancellationRequested && !_disposed; topicIdx++)
            {
                var topic = topics[(_currentTopicIndex + topicIdx) % topics.Length];
                var partitions = _subscriptions[topic];

                for (var partIdx = 0; partIdx < partitions.Length && !cancellationToken.IsCancellationRequested && !_disposed; partIdx++)
                {
                    var partition = partitions[(_currentPartitionIndex + partIdx) % partitions.Length];
                    var message = await _queue.ConsumeAsync(topic, partition, cancellationToken);

                    if (message != null)
                    {
                        _currentTopicIndex = (_currentTopicIndex + topicIdx + 1) % topics.Length;
                        _currentPartitionIndex = (_currentPartitionIndex + partIdx + 1) % partitions.Length;
                        yield return message;
                        break;
                    }
                }
            }

            // 如果所有主题和分区都没有消息，稍作等待避免空转
            if (!_paused)
            {
                await Task.Yield();
            }
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Memory 队列不需要手动提交
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        _paused = true;
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        _paused = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.UnregisterConsumer(Name, _subscriptions);
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
