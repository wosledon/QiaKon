using System.Collections.Concurrent;
using Confluent.Kafka;

namespace QiaKon.Queue.Kafka;

/// <summary>
/// Kafka 队列消息实现
/// </summary>
internal sealed class KafkaQueueMessage : IQueueMessage
{
    private static readonly Dictionary<string, string> EmptyHeaders = new();

    public required string Id { get; init; }
    public required string Topic { get; init; }
    public required ReadOnlyMemory<byte> Body { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;
    public long Offset { get; init; }
    public int Partition { get; init; }

    public static KafkaQueueMessage FromConsumeResult(ConsumeResult<string, byte[]> result)
    {
        var headers = new Dictionary<string, string>();
        if (result.Message.Headers != null)
        {
            foreach (var header in result.Message.Headers)
            {
                headers[header.Key] = System.Text.Encoding.UTF8.GetString(header.GetValueBytes());
            }
        }

        return new KafkaQueueMessage
        {
            Id = result.Message.Key ?? Guid.NewGuid().ToString("N"),
            Topic = result.Topic,
            Body = result.Message.Value,
            Timestamp = result.Message.Timestamp.UtcDateTime,
            Headers = headers,
            Offset = result.Offset.Value,
            Partition = result.Partition.Value
        };
    }
}

/// <summary>
/// Kafka 队列实现
/// </summary>
public sealed class KafkaQueue : IQueue, IDisposable
{
    private readonly KafkaQueueOptions _options;
    private readonly IProducer<string, byte[]> _producer;
    private readonly ConcurrentDictionary<string, Lazy<KafkaConsumer>> _consumers = new();
    private readonly ConcurrentDictionary<string, Lazy<IProducer<string, byte[]>>> _producers = new();
    private bool _disposed;

    public string Name { get; }
    public QueueType Type => QueueType.Kafka;

    public KafkaQueue(KafkaQueueOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrEmpty(options.BootstrapServers))
            throw new ArgumentException("BootstrapServers is required", nameof(options));

        Name = $"KafkaQueue_{options.BootstrapServers}";

        var producerConfig = CreateProducerConfig(options);
        _producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IQueueProducer> CreateProducerAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(KafkaQueue));
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

        var producer = _producers.GetOrAdd(name, n =>
            new Lazy<IProducer<string, byte[]>>(() =>
            {
                var config = CreateProducerConfig(_options);
                return new ProducerBuilder<string, byte[]>(config).Build();
            })).Value;

        return Task.FromResult<IQueueProducer>(new KafkaQueueProducer(name, producer));
    }

    public Task<IQueueConsumer> CreateConsumerAsync(
        string name,
        string groupId,
        string[] topics,
        int[]? partitions = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(KafkaQueue));
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrEmpty(groupId)) throw new ArgumentNullException(nameof(groupId));
        if (topics == null || topics.Length == 0) throw new ArgumentNullException(nameof(topics));

        var consumer = _consumers.GetOrAdd(name, n =>
            new Lazy<KafkaConsumer>(() => CreateConsumer(groupId, topics, partitions))).Value;

        return Task.FromResult<IQueueConsumer>(new KafkaQueueConsumer(name, consumer));
    }

    public Task<int> GetPartitionCountAsync(string topic, CancellationToken cancellationToken = default)
    {
        // Kafka 队列不支持在创建前获取分区数，需要连接后获取
        // 这里返回配置的默认值，实际分区数由 Kafka 集群决定
        return Task.FromResult(_options.DefaultPartitionCount > 0 ? _options.DefaultPartitionCount : 4);
    }

    private KafkaConsumer CreateConsumer(string groupId, string[] topics, int[]? partitions = null)
    {
        var config = CreateConsumerConfig(groupId);
        return new KafkaConsumer(config, topics, partitions, _options);
    }

    private ProducerConfig CreateProducerConfig(KafkaQueueOptions options)
    {
        var acks = options.Acks switch
        {
            KafkaAcks.None => Confluent.Kafka.Acks.None,
            KafkaAcks.Leader => Confluent.Kafka.Acks.Leader,
            KafkaAcks.All => Confluent.Kafka.Acks.All,
            _ => Confluent.Kafka.Acks.Leader
        };

        return new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = acks,
            LingerMs = (int)options.Linger.TotalMilliseconds,
            BatchSize = options.BatchSize,
            MessageTimeoutMs = (int)TimeSpan.FromMinutes(5).TotalMilliseconds
        };
    }

    private ConsumerConfig CreateConsumerConfig(string groupId)
    {
        var effectiveGroupId = string.IsNullOrEmpty(_options.GroupIdPrefix)
            ? groupId
            : $"{_options.GroupIdPrefix}-{groupId}";

        return new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = effectiveGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = (int)_options.SessionTimeout.TotalMilliseconds,
            MaxPollIntervalMs = (int)_options.MaxPollInterval.TotalMilliseconds
        };
    }

    internal async Task ProduceAsync(string topic, ReadOnlyMemory<byte> message, string? key, int partition, Dictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        var kafkaMessage = new Message<string, byte[]>
        {
            Key = key ?? Guid.NewGuid().ToString("N"),
            Value = message.ToArray(),
            Headers = CreateHeaders(headers)
        };

        if (partition >= 0)
        {
            var tp = new TopicPartition(topic, new Partition(partition));
            await _producer.ProduceAsync(tp, kafkaMessage, cancellationToken);
        }
        else
        {
            await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
        }
    }

    internal async Task ProduceManyAsync(string topic, IEnumerable<ReadOnlyMemory<byte>> messages, string? key, int partition, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProduceAsync(topic, message, key, partition, null, cancellationToken);
        }
    }

    private static Headers CreateHeaders(Dictionary<string, string>? headers)
    {
        var result = new Headers();
        if (headers != null)
        {
            foreach (var (k, v) in headers)
            {
                result.Add(k, System.Text.Encoding.UTF8.GetBytes(v));
            }
        }
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();

        foreach (var consumer in _consumers.Values)
        {
            consumer.Value.Dispose();
        }
        _consumers.Clear();

        foreach (var producer in _producers.Values)
        {
            producer.Value.Flush(TimeSpan.FromSeconds(10));
            producer.Value.Dispose();
        }
        _producers.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}
