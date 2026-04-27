using System.Collections.Concurrent;
using System.Threading.Channels;

namespace QiaKon.Queue.Memory;

/// <summary>
/// 基于 Channel 的内存队列实现（支持 Partition）
/// </summary>
public sealed class MemoryQueue : IQueue
{
    private readonly MemoryQueueOptions _options;
    // (topic, partition) -> ChannelWrapper
    private readonly ConcurrentDictionary<(string Topic, int Partition), ChannelWrapper> _topicPartitions = new();
    // topic -> partition count
    private readonly ConcurrentDictionary<string, int> _topicPartitionCounts = new();
    private readonly ConcurrentDictionary<string, MemoryQueueConsumer> _consumers = new();
    private readonly ConcurrentDictionary<string, MemoryQueueProducer> _producers = new();
    private bool _disposed;

    public string Name { get; } = $"MemoryQueue_{Guid.NewGuid():N}";
    public QueueType Type => QueueType.Memory;

    public MemoryQueue(MemoryQueueOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_options.DefaultTopic))
        {
            EnsurePartitions(_options.DefaultTopic, _options.PartitionCount);
        }
        return Task.CompletedTask;
    }

    public Task<IQueueProducer> CreateProducerAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MemoryQueue));
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

        var producer = _producers.GetOrAdd(name, n => new MemoryQueueProducer(n, this));
        return Task.FromResult<IQueueProducer>(producer);
    }

    public Task<IQueueConsumer> CreateConsumerAsync(
        string name,
        string groupId,
        string[] topics,
        int[]? partitions = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MemoryQueue));
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
        if (topics == null || topics.Length == 0) throw new ArgumentNullException(nameof(topics));

        // 确保所有 topic 的分区都已创建
        foreach (var topic in topics)
        {
            var partitionCount = partitions?.Length ?? _options.PartitionCount;
            EnsurePartitions(topic, partitionCount);
        }

        // 构建消费订阅：topic -> partitions
        var subscriptions = new Dictionary<string, int[]>();
        foreach (var topic in topics)
        {
            var topicPartitionCount = _topicPartitionCounts.TryGetValue(topic, out var count) ? count : _options.PartitionCount;
            if (partitions != null && partitions.Length > 0)
            {
                // 使用指定的 partitions
                subscriptions[topic] = partitions.Where(p => p >= 0 && p < topicPartitionCount).ToArray();
            }
            else
            {
                // 订阅所有 partitions
                subscriptions[topic] = Enumerable.Range(0, topicPartitionCount).ToArray();
            }
        }

        var consumer = new MemoryQueueConsumer(name, groupId, subscriptions, this);
        _consumers.TryAdd(name, consumer);

        // 注册消费者到各分区
        foreach (var (topic, consumerPartitions) in subscriptions)
        {
            foreach (var partition in consumerPartitions)
            {
                var key = (topic, partition);
                if (_topicPartitions.TryGetValue(key, out var channel))
                {
                    channel.RegisterConsumer(name);
                }
            }
        }

        return Task.FromResult<IQueueConsumer>(consumer);
    }

    public Task<int> GetPartitionCountAsync(string topic, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_topicPartitionCounts.TryGetValue(topic, out var count) ? count : _options.PartitionCount);
    }

    internal void EnsurePartitions(string topic, int count)
    {
        // 更新 topic 的分区数
        _topicPartitionCounts.TryAdd(topic, count);

        // 创建所有分区 channel
        for (var i = 0; i < count; i++)
        {
            var key = (topic, i);
            _topicPartitions.GetOrAdd(key, _ => CreateChannelWrapper());
        }
    }

    private ChannelWrapper CreateChannelWrapper()
    {
        var channel = _options.AllowMultipleConsumers
            ? Channel.CreateBounded<ChannelQueueMessage>(new BoundedChannelOptions(_options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            })
            : Channel.CreateBounded<ChannelQueueMessage>(new BoundedChannelOptions(_options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

        return new ChannelWrapper(channel);
    }

    internal ChannelWrapper? GetChannel(string topic, int partition)
    {
        _topicPartitions.TryGetValue((topic, partition), out var channel);
        return channel;
    }

    internal int GetPartitionCount(string topic)
    {
        return _topicPartitionCounts.TryGetValue(topic, out var count) ? count : _options.PartitionCount;
    }

    internal async Task ProduceAsync(string topic, ReadOnlyMemory<byte> message, string? key, int partition, Dictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        var partitionCount = GetPartitionCount(topic);
        int targetPartition;

        if (partition >= 0 && partition < partitionCount)
        {
            targetPartition = partition;
        }
        else
        {
            // 使用 key 的哈希值来选择分区
            targetPartition = !string.IsNullOrEmpty(key)
                ? Math.Abs(key.GetHashCode()) % partitionCount
                : Random.Shared.Next(partitionCount);
        }

        var channel = GetChannel(topic, targetPartition);
        if (channel == null)
        {
            throw new QueueException($"Channel not found for topic {topic} partition {targetPartition}", Name, topic);
        }

        var queueMessage = ChannelQueueMessage.Create(topic, message, key, headers, targetPartition);
        await channel.WriteAsync(queueMessage);
    }

    internal async Task<ChannelQueueMessage?> ConsumeAsync(string topic, int partition, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.ConsumeTimeout);

        try
        {
            var channel = GetChannel(topic, partition);
            if (channel == null) return null;

            return await channel.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    internal void UnregisterConsumer(string consumerId, Dictionary<string, int[]> subscriptions)
    {
        foreach (var (topic, partitions) in subscriptions)
        {
            foreach (var partition in partitions)
            {
                if (_topicPartitions.TryGetValue((topic, partition), out var channel))
                {
                    channel.UnregisterConsumer(consumerId);
                }
            }
        }
        _consumers.TryRemove(consumerId, out _);
    }

    internal void UnregisterProducer(string producerId)
    {
        _producers.TryRemove(producerId, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var channel in _topicPartitions.Values)
        {
            channel.CompleteWriter();
        }
        _topicPartitions.Clear();
        _topicPartitionCounts.Clear();
        _consumers.Clear();
        _producers.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
