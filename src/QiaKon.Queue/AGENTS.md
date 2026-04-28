# QiaKon.Queue - AGENTS.md

> **模块**: 消息队列抽象层  
> **职责**: 提供统一的消息队列接口和实现  
> **依赖**: `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Api`, 需要异步处理的业务模块

---

## 一、模块职责

本模块提供消息队列的抽象层，支持异步消息处理、事件驱动架构和解耦模块间通信。

**核心职责**:
- 定义 `IQueue` 基础接口
- 支持 Memory（开发测试）和 Kafka（生产）实现
- 消息发布与订阅
- 消费者组管理
- 消息重试与死信队列

---

## 二、核心接口

### 2.1 队列接口

```csharp
public interface IQueue : IDisposable, IAsyncDisposable
{
    string Name { get; }
    
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
    Task PublishBatchAsync<T>(IReadOnlyList<T> messages, CancellationToken ct = default) where T : class;
    
    IAsyncEnumerable<T> SubscribeAsync<T>(
        string consumerGroup,
        CancellationToken ct = default) where T : class;
}
```

### 2.2 消息信封

```csharp
public class Envelope<T>
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public T Payload { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; }
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
```

### 2.3 消费者接口

```csharp
public interface IConsumer<T> where T : class
{
    Task HandleAsync(Envelope<T> message, CancellationToken ct = default);
}
```

---

## 三、实现模块

### 3.1 已实现队列

| 模块                  | 说明     | 适用场景           |
| --------------------- | -------- | ------------------ |
| `QiaKon.Queue.Memory` | 内存队列 | 开发测试、单机部署 |

### 3.2 规划中队列

| 模块                 | 说明           | 优先级 |
| -------------------- | -------------- | ------ |
| `QiaKon.Queue.Kafka` | Kafka 消息队列 | P1     |

---

## 四、Memory 队列规范

### 4.1 实现要点

```csharp
public class MemoryQueue : IQueue
{
    private readonly Channel<Envelope<object>> _channel;
    private readonly List<Func<Envelope<object>, Task>> _subscribers = new();
    
    public MemoryQueue(MemoryQueueOptions options)
    {
        _channel = Channel.CreateBounded<Envelope<object>>(options.Capacity);
    }
    
    public async Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        var envelope = new Envelope<T>
        {
            Payload = message,
            EventType = typeof(T).Name
        };
        
        await _channel.Writer.WriteAsync(envelope, ct);
        
        // 通知订阅者
        foreach (var subscriber in _subscribers)
        {
            await subscriber(envelope);
        }
    }
}
```

### 4.2 配置示例

```json
{
  "MemoryQueue": {
    "Capacity": 10000,
    "EnableDeadLetter": true,
    "DeadLetterCapacity": 1000
  }
}
```

---

## 五、Kafka 队列规范（规划中）

### 5.1 配置示例

```json
{
  "KafkaQueue": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "qiakon-consumer-group",
    "AutoOffsetReset": "Earliest",
    "EnableAutoCommit": false,
    "MaxPollIntervalMs": 300000,
    "Topics": {
      "DocumentEvents": "qiakon.document.events",
      "GraphEvents": "qiakon.graph.events",
      "RetrievalEvents": "qiakon.retrieval.events"
    }
  }
}
```

### 5.2 消费者组管理

- 每个模块使用独立的消费者组
- 支持水平扩展（多个实例共享消费者组）
- 手动提交 Offset 确保消息处理完成

---

## 六、消息重试与死信队列

### 6.1 重试策略

```csharp
public class RetryPolicy
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    public BackoffType Backoff { get; init; } = BackoffType.Exponential;
}
```

### 6.2 死信队列

- 消息重试失败后移入死信队列
- 死信队列消息保留 7 天
- 提供死信队列管理接口（重新处理、删除）

---

## 七、使用场景

### 7.1 文档处理异步化

```csharp
// 文档上传后发布事件
await _queue.PublishAsync(new DocumentUploadedEvent
{
    DocumentId = document.Id,
    UploadedBy = userId,
    Timestamp = DateTime.UtcNow
});

// 异步消费者处理文档解析
public class DocumentProcessingConsumer : IConsumer<DocumentUploadedEvent>
{
    public async Task HandleAsync(Envelope<DocumentUploadedEvent> message, CancellationToken ct)
    {
        // 1. 解析文档
        // 2. 分块
        // 3. 生成嵌入
        // 4. 存储向量
    }
}
```

### 7.2 事件驱动架构

```
文档上传 → DocumentUploadedEvent
              ↓
    ┌─────────┴─────────┐
    ↓                   ↓
文档解析消费者      审计日志消费者
    ↓                   ↓
分块 & 嵌入         记录审计日志
    ↓
向量存储
```

---

## 八、开发规范

### 8.1 添加新队列实现

1. 实现 `IQueue` 接口
2. 创建配置类 `{Name}QueueOptions`
3. 实现消费者组管理
4. 实现重试和死信队列
5. 编写 `ServiceCollectionExtensions` 注册扩展
6. 编写集成测试

### 8.2 消息设计原则

- **不可变性**: 消息对象应该是不可变的
- **自包含**: 消息包含处理所需的所有信息
- **幂等性**: 消费者应该能处理重复消息
- **版本控制**: 消息结构变更时增加版本号

---

## 九、测试要求

### 9.1 单元测试

- 消息发布逻辑
- 消费者处理逻辑
- 重试机制

### 9.2 集成测试

- Memory 队列完整流程
- Kafka 队列完整流程（使用 Testcontainers）
- 消费者组协调

---

## 十、注意事项

1. **消息顺序**: 不保证全局顺序，仅保证分区内顺序
2. **消息大小**: 单条消息限制 1MB
3. **背压处理**: 使用有界 Channel 防止内存溢出
4. **优雅关闭**: 应用关闭时完成正在处理的消息

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
