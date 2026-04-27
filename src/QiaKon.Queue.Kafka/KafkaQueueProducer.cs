using Confluent.Kafka;

namespace QiaKon.Queue.Kafka;

/// <summary>
/// Kafka 队列生产者
/// </summary>
public sealed class KafkaQueueProducer : IQueueProducer
{
    private readonly IProducer<string, byte[]> _producer;
    private bool _disposed;

    public string Name { get; }

    public KafkaQueueProducer(string name, IProducer<string, byte[]> producer)
    {
        Name = name;
        _producer = producer;
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
        if (_disposed) throw new ObjectDisposedException(nameof(KafkaQueueProducer));
        if (string.IsNullOrEmpty(topic)) throw new ArgumentNullException(nameof(topic));

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

    public async Task ProduceManyAsync(
        string topic,
        IEnumerable<ReadOnlyMemory<byte>> messages,
        int partition = -1,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(KafkaQueueProducer));
        if (string.IsNullOrEmpty(topic)) throw new ArgumentNullException(nameof(topic));
        if (messages == null) throw new ArgumentNullException(nameof(messages));

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProduceAsync(topic, message, null, partition, null, cancellationToken);
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _producer.Flush(cancellationToken);
        return Task.CompletedTask;
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
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
