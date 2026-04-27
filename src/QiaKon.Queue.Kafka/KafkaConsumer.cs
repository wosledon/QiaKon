using Confluent.Kafka;

namespace QiaKon.Queue.Kafka;

/// <summary>
/// Kafka 消费者包装器
/// </summary>
public sealed class KafkaConsumer : IDisposable
{
    private readonly IConsumer<string, byte[]> _consumer;
    private readonly string[] _topics;
    private readonly int[]? _partitions;
    private readonly KafkaQueueOptions _options;
    private bool _disposed;
    private bool _paused;

    public KafkaConsumer(ConsumerConfig config, string[] topics, int[]? partitions, KafkaQueueOptions options)
    {
        _topics = topics;
        _partitions = partitions;
        _options = options;

        _consumer = new ConsumerBuilder<string, byte[]>(config).Build();

        if (partitions != null && partitions.Length > 0)
        {
            var topicPartitions = topics
                .SelectMany(t => partitions.Select(p => new TopicPartition(t, new Partition(p))))
                .ToList();
            _consumer.Assign(topicPartitions);
        }
        else
        {
            _consumer.Subscribe(topics);
        }
    }

    public string[] Topics => _topics;
    public int[]? Partitions => _partitions;

    public ConsumeResult<string, byte[]>? Consume(CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(KafkaConsumer));
        if (_paused) return null;

        try
        {
            var result = _consumer.Consume(TimeSpan.FromSeconds(1));
            return result;
        }
        catch (ConsumeException)
        {
            return null;
        }
    }

    public void Commit()
    {
        if (_disposed) return;
        try
        {
            _consumer.Commit();
        }
        catch (KafkaException)
        {
            // 忽略提交错误
        }
    }

    public void Pause()
    {
        _paused = true;
        _consumer.Pause(_consumer.Assignment);
    }

    public void Resume()
    {
        _paused = false;
        _consumer.Resume(_consumer.Assignment);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _consumer.Close();
        }
        catch { }

        _consumer.Dispose();
    }
}
