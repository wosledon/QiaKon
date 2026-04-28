# QiaKon.Queue.Kafka - AGENTS.md

> **模块**: Kafka 消息队列  
> **职责**: 基于 Apache Kafka 的分布式队列实现  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Queue`  
> **被依赖**: `QiaKon.Queue`, 生产环境

---

## 一、模块职责

本模块提供基于 Apache Kafka 的分布式队列实现，适用于生产环境和大规模部署。

**核心职责**:
- `KafkaQueue` 实现
- 消费者组管理
- 分区与偏移量管理
- 消息重试与死信队列

---

## 二、核心实现

### 2.1 KafkaQueue

```csharp
public class KafkaQueue : IQueue
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly IConsumer<string, byte[]> _consumer;
}
```

### 2.2 KafkaQueueOptions

```csharp
public class KafkaQueueOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "qiakon-consumer-group";
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;
    public bool EnableAutoCommit { get; set; } = false;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public IDictionary<string, string> Topics { get; set; } = new();
}
```

---

## 三、开发规范

### 3.1 配置示例

```json
{
  "KafkaQueue": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "qiakon-consumer-group",
    "Topics": {
      "DocumentEvents": "qiakon.document.events",
      "GraphEvents": "qiakon.graph.events"
    }
  }
}
```

### 3.2 消费者组

- 每个模块使用独立的消费者组
- 支持水平扩展
- 手动提交 Offset

---

## 四、测试要求

- 消息发布和订阅（使用 Testcontainers）
- 消费者组协调
- 分区负载均衡
- 故障恢复

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
