# QiaKon.Queue.Memory - AGENTS.md

> **模块**: 内存消息队列  
> **职责**: 基于 Channel 的内存队列实现  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Queue`  
> **被依赖**: `QiaKon.Queue`, 开发测试环境

---

## 一、模块职责

本模块提供基于 `System.Threading.Channels` 的内存队列实现，适用于开发测试和单机部署。

**核心职责**:
- `MemoryQueue` 实现
- 生产者/消费者模式
- 背压控制
- 死信队列

---

## 二、核心实现

### 2.1 MemoryQueue

```csharp
public class MemoryQueue : IQueue
{
    private readonly Channel<Envelope<object>> _channel;
    private readonly List<Func<Envelope<object>, Task>> _subscribers = new();
    
    public MemoryQueue(MemoryQueueOptions options)
    {
        _channel = Channel.CreateBounded<Envelope<object>>(new BoundedChannelOptions(options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }
}
```

### 2.2 MemoryQueueOptions

```csharp
public class MemoryQueueOptions
{
    public int Capacity { get; set; } = 10000;
    public bool EnableDeadLetter { get; set; } = true;
    public int DeadLetterCapacity { get; set; } = 1000;
}
```

---

## 三、开发规范

### 3.1 发布消息

```csharp
await queue.PublishAsync(new DocumentUploadedEvent
{
    DocumentId = document.Id,
    UploadedBy = userId
});
```

### 3.2 订阅消息

```csharp
await foreach (var message in queue.SubscribeAsync<DocumentUploadedEvent>("consumer-group"))
{
    await ProcessMessageAsync(message);
}
```

---

## 四、测试要求

- 消息发布和订阅
- 背压控制
- 死信队列
- 并发安全

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
