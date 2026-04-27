# QiaKon.Queue 消息队列模块

统一的消息队列接口，提供 Memory（内存）和 Kafka 两种实现，支持 Partition 分区概念。

## 模块结构

```
QiaKon.Queue          # 核心接口与抽象
QiaKon.Queue.Memory    # 基于 Channel 的内存队列实现
QiaKon.Queue.Kafka     # 基于 Confluent.Kafka 的 Kafka 实现
```

## 核心接口

### IQueue - 队列工厂
```csharp
public interface IQueue : IAsyncDisposable, IDisposable
{
    string Name { get; }
    QueueType Type { get; }

    Task<IQueueProducer> CreateProducerAsync(string name, CancellationToken cancellationToken = default);
    Task<IQueueConsumer> CreateConsumerAsync(
        string name,
        string groupId,
        string[] topics,
        int[]? partitions = null,
        CancellationToken cancellationToken = default);
    Task<int> GetPartitionCountAsync(string topic, CancellationToken cancellationToken = default);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
```

### IQueueProducer - 生产者
```csharp
public interface IQueueProducer : IAsyncDisposable, IDisposable
{
    string Name { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ProduceAsync(string topic, ReadOnlyMemory<byte> message, string? key = null,
        int partition = -1, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
    Task ProduceManyAsync(string topic, IEnumerable<ReadOnlyMemory<byte>> messages,
        int partition = -1, CancellationToken cancellationToken = default);
    Task FlushAsync(CancellationToken cancellationToken = default);
}
```

### IQueueConsumer - 消费者
```csharp
public interface IQueueConsumer : IAsyncDisposable, IDisposable
{
    string Name { get; }
    string GroupId { get; }
    IReadOnlyList<string> Topics { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<IQueueMessage> ConsumeAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
}
```

### IQueueMessage - 消息
```csharp
public interface IQueueMessage
{
    string Id { get; }
    string Topic { get; }
    int Partition { get; }  // 分区号（-1 表示未分配）
    ReadOnlyMemory<byte> Body { get; }
    DateTimeOffset Timestamp { get; }
    IReadOnlyDictionary<string, string> Headers { get; }
}
```

## 配置选项

### MemoryQueueOptions
| 属性                     | 默认值 | 说明                 |
| ------------------------ | ------ | -------------------- |
| `PartitionCount`         | 4      | 每个主题的默认分区数 |
| `ChannelCapacity`        | 10000  | 每个分区的通道容量   |
| `AllowMultipleConsumers` | false  | 是否允许多消费者     |
| `ConsumeTimeout`         | 1s     | 消费超时时间         |

### KafkaQueueOptions
| 属性                    | 默认值 | 说明                                     |
| ----------------------- | ------ | ---------------------------------------- |
| `BootstrapServers`      | (必需) | Kafka 服务器地址                         |
| `GroupIdPrefix`         | null   | 消费者组 ID 前缀                         |
| `DefaultPartitionCount` | -1     | 默认分区数（-1 表示使用 Kafka 集群配置） |
| `AutoCommitInterval`    | 5s     | 自动提交间隔                             |
| `SessionTimeout`        | 30s    | 消费者会话超时                           |
| `MaxPollInterval`       | 5min   | 最大 poll 间隔                           |
| `Acks`                  | Leader | 消息确认级别                             |
| `Linger`                | 5ms    | 生产者 linger 时间                       |
| `BatchSize`             | 16384  | 批量大小（字节）                         |

## Partition 支持

两种队列实现都支持 Partition 概念：

### 生产者
```csharp
// 发送到指定分区
await producer.ProduceAsync("topic1", data, key: "key1", partition: 2);

// 不指定分区时，使用 key 的哈希值自动选择分区
await producer.ProduceAsync("topic1", data, key: "key1");

// 完全自动选择（随机分区）
await producer.ProduceAsync("topic1", data);

// 批量发送（partition = -1 表示自动）
await producer.ProduceManyAsync("topic1", messages);
```

### 消费者
```csharp
// 订阅指定主题的所有分区
var consumer = await queue.CreateConsumerAsync("c1", "group1", ["topic1"]);

// 订阅指定主题的指定分区
var consumer = await queue.CreateConsumerAsync("c1", "group1", ["topic1"], partitions: [0, 1]);

// 订阅多个主题的特定分区
var consumer = await queue.CreateConsumerAsync("c1", "group1", ["topic1", "topic2"], partitions: [0, 2]);

// 获取主题的分区数
var count = await queue.GetPartitionCountAsync("topic1");
```

// 不指定分区时，使用 key 的哈希值自动选择分区
await producer.ProduceAsync("topic1", data, key: "key1");

// 完全自动选择
await producer.ProduceAsync("topic1", data);
```

### 消费者
```csharp
// 订阅指定主题的所有分区
var consumer = await queue.CreateConsumerAsync("c1", "group1", ["topic1"]);

// 订阅指定主题的指定分区
var consumer = await queue.CreateConsumerAsync("c1", "group1", ["topic1"], partitions: [0, 1]);

// 获取主题的分区数
var count = await queue.GetPartitionCountAsync("topic1");
```

## 使用示例

### 内存队列

```csharp
// 注册服务
services.AddMemoryQueue(opts => {
    opts.ChannelCapacity = 10000;
    opts.AllowMultipleConsumers = false;
});

// 注入使用
public class MyService(IQueue queue)
{
    public async Task ProduceAsync()
    {
        var producer = await queue.CreateProducerAsync("producer1");
        var data = System.Text.Encoding.UTF8.GetBytes("Hello");
        await producer.ProduceAsync("topic1", data);
    }

    public async Task ConsumeAsync()
    {
        var consumer = await queue.CreateConsumerAsync("consumer1", "group1", ["topic1"]);
        await foreach (var msg in consumer.ConsumeAsync())
        {
            var text = System.Text.Encoding.UTF8.GetString(msg.Body.Span);
            Console.WriteLine($"Received: {text}");
            await consumer.CommitAsync();
        }
    }
}
```

### Kafka 队列

```csharp
// 注册服务
services.AddKafkaQueue(opts => {
    opts.BootstrapServers = "localhost:9092";
    opts.GroupIdPrefix = "myapp";
    opts.Acks = KafkaAcks.All;
});

// 注入使用
public class MyService(IQueue queue)
{
    public async Task ProduceAsync()
    {
        var producer = await queue.CreateProducerAsync("producer1");
        var data = System.Text.Encoding.UTF8.GetBytes("Hello");
        await producer.ProduceAsync("topic1", data, key: "key1");
        await producer.FlushAsync();
    }

    public async Task ConsumeAsync()
    {
        var consumer = await queue.CreateConsumerAsync("consumer1", "group1", ["topic1"]);
        await foreach (var msg in consumer.ConsumeAsync())
        {
            var text = System.Text.Encoding.UTF8.GetString(msg.Body.Span);
            Console.WriteLine($"Received from partition {msg.partition}: {text}");
        }
    }
}
```

## 扩展方式

如需添加新的队列实现（如 RabbitMQ、RocketMQ），只需：

1. 创建新项目 `QiaKon.Queue.{Provider}`
2. 实现 `IQueue`、`IQueueProducer`、`IQueueConsumer` 接口
3. 提供 `Add{Provider}Queue` 扩展方法