# QiaKon.Connector - AGENTS.md

> **模块**: 连接器抽象层  
> **职责**: 定义外部系统连接器的统一接口和生命周期管理  
> **依赖**: `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Api`, `QiaKon.Graph.Engine.*`, `QiaKon.Retrieval.*`

---

## 一、模块职责

本模块提供外部系统连接的抽象层，统一管理各类外部系统（HTTP API、数据库、消息队列）的连接生命周期。

**核心职责**:
- 定义 `IConnector` 基础接口
- 管理连接器生命周期（初始化、健康检查、关闭）
- 提供连接器注册表和管理器
- 实现配置驱动的连接器创建

---

## 二、核心接口

### 2.1 连接器接口

```csharp
public interface IConnector : IDisposable, IAsyncDisposable
{
    string Name { get; }
    ConnectorType Type { get; }
    ConnectorState State { get; }
    
    Task InitializeAsync(CancellationToken ct = default);
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);
}

public enum ConnectorType { Http, Npgsql, Kafka }
public enum ConnectorState { Disconnected, Connecting, Connected, Healthy, Unhealthy, Closed }
```

### 2.2 连接器工厂

```csharp
public interface IConnectorFactory<TConnector> where TConnector : IConnector
{
    TConnector Create(ConnectorOptions options);
}
```

### 2.3 连接器管理器

```csharp
public interface IConnectorManager
{
    void Register(IConnector connector);
    IConnector? Get(string name);
    Task InitializeAllAsync(CancellationToken ct = default);
    Task CloseAllAsync(CancellationToken ct = default);
    Task<Dictionary<string, HealthCheckResult>> HealthCheckAllAsync(CancellationToken ct = default);
}
```

---

## 三、实现模块

### 3.1 已实现连接器

| 模块                      | 接口               | 实现              | 说明                          |
| ------------------------- | ------------------ | ----------------- | ----------------------------- |
| `QiaKon.Connector.Http`   | `IHttpConnector`   | `HttpConnector`   | HTTP API 调用，支持端点配置化 |
| `QiaKon.Connector.Npgsql` | `INpgsqlConnector` | `NpgsqlConnector` | PostgreSQL 数据库连接         |

### 3.2 规划中连接器

| 模块                     | 接口              | 说明           | 优先级 |
| ------------------------ | ----------------- | -------------- | ------ |
| `QiaKon.Connector.Kafka` | `IKafkaConnector` | Kafka 消息队列 | P2     |

---

## 四、HTTP 连接器规范

### 4.1 接口定义

```csharp
public interface IHttpConnector : IConnector
{
    Task<HttpResponseMessage> CallAsync(
        string endpointName, 
        IDictionary<string, object>? parameters = null, 
        CancellationToken ct = default);
}
```

### 4.2 配置项

| 配置项        | 类型                                 | 说明         | 默认值        |
| ------------- | ------------------------------------ | ------------ | ------------- |
| `BaseUrl`     | `string`                             | API 基础地址 | 必填          |
| `Endpoints`   | `Dictionary<string, EndpointConfig>` | 端点配置     | 必填          |
| `Timeout`     | `TimeSpan`                           | 请求超时     | 30s           |
| `RetryPolicy` | `RetryStrategy`                      | 重试策略     | 指数退避 3 次 |

### 4.3 端点配置示例

```json
{
  "HttpConnector": {
    "BaseUrl": "https://api.example.com",
    "Endpoints": {
      "GetUser": {
        "Path": "/users/{id}",
        "Method": "GET",
        "Headers": { "Authorization": "Bearer {token}" }
      },
      "CreateUser": {
        "Path": "/users",
        "Method": "POST",
        "BodyMapping": { "name": "UserName", "email": "Email" }
      }
    }
  }
}
```

---

## 五、Npgsql 连接器规范

### 5.1 接口定义

```csharp
public interface INpgsqlConnector : IConnector
{
    Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct = default);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
    Task<List<T>> QueryListAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default);
}
```

### 5.2 配置项

| 配置项             | 类型       | 说明                  | 默认值 |
| ------------------ | ---------- | --------------------- | ------ |
| `ConnectionString` | `string`   | PostgreSQL 连接字符串 | 必填   |
| `CommandTimeout`   | `TimeSpan` | 命令超时              | 30s    |
| `MaxPoolSize`      | `int`      | 连接池最大连接数      | 100    |

---

## 六、连接器生命周期

### 6.1 状态流转

```
Disconnected → Connecting → Connected → Healthy
                                       ↓
                                   Unhealthy
                                       ↓
                                   Closed
```

### 6.2 生命周期方法

| 方法               | 触发时机   | 职责                   |
| ------------------ | ---------- | ---------------------- |
| `InitializeAsync`  | 应用启动时 | 建立连接、验证配置     |
| `HealthCheckAsync` | 定时/按需  | 检测连接健康状态       |
| `CloseAsync`       | 应用关闭时 | 优雅关闭连接、释放资源 |

---

## 七、开发规范

### 7.1 添加新连接器流程

1. 创建新项目 `QiaKon.Connector.{Name}`
2. 实现 `IConnector` 接口
3. 创建配置类 `{Name}ConnectorOptions`
4. 实现工厂类 `{Name}ConnectorFactory`
5. 编写 `ServiceCollectionExtensions` 注册扩展
6. 编写单元测试（覆盖率 ≥ 80%）
7. 更新本文档

### 7.2 错误处理

- 连接失败：抛出 `ConnectorException`，包含详细错误信息
- 超时处理：使用 `CancellationToken` 控制超时
- 重试策略：实现指数退避重试

### 7.3 健康检查

- 实现 `HealthCheckAsync` 方法
- 返回 `HealthCheckResult`，包含状态、延迟、错误信息
- 健康检查不应影响正常业务流程

---

## 八、测试要求

### 8.1 单元测试

- 连接器初始化逻辑
- 健康检查逻辑
- 关闭逻辑
- 错误处理逻辑

### 8.2 集成测试

- 真实外部系统连接测试（使用 Testcontainers）
- 连接池管理测试
- 并发连接测试

---

## 九、注意事项

1. **资源释放**: 必须正确实现 `Dispose` 和 `DisposeAsync`
2. **线程安全**: 连接器可能被多线程并发使用
3. **配置验证**: 初始化时验证配置完整性
4. **日志记录**: Warning 级别记录连接异常，Info 级别记录连接状态变更

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
