# QiaKon.Connector

配置驱动的数据访问连接器，支持 HTTP API 和 PostgreSQL 数据库的模板化调用。

## 设计理念

**配置驱动**：所有 HTTP 端点和数据库查询都通过配置定义，业务代码只需调用模板名称并传入参数，无需编写具体的 HTTP 请求或 SQL 语句。

### 核心优势

- **零代码调用**：业务代码不写 SQL/HTTP 请求，只传参数
- **配置集中管理**：所有端点/SQL 在配置文件中维护
- **易于扩展**：新增 API/查询只需加配置，不用改代码
- **类型安全**：支持泛型反序列化，自动映射结果
- **模板变量**：URL/Body/SQL 都支持占位符

## 架构概览

```
QiaKon.Connector (基础抽象层)
├── IConnector - 连接器基础接口
├── IHttpConnector - HTTP 连接器接口
├── IDbConnector - 数据库连接器接口
├── IConnectorFactory - 工厂接口（扩展点）
├── IConnectorRegistry - 注册表
├── ConnectorManager - 配置驱动管理器
└── 配置模型
    ├── HttpEndpointConfig - HTTP 端点配置
    ├── DbQueryTemplateConfig - 数据库查询模板配置
    └── DbParameterConfig - 数据库参数配置

QiaKon.Connector.Http (HTTP 实现)
├── HttpConnector - HTTP 连接器实现
├── HttpConnectorOptions - HTTP 配置选项
└── HttpConnectorFactory - HTTP 工厂

QiaKon.Connector.Npgsql (PostgreSQL 实现)
├── NpgsqlConnector - Npgsql 连接器实现
├── NpgsqlConnectorOptions - Npgsql 配置选项
└── NpgsqlConnectorFactory - Npgsql 工厂
```

## 快速开始

### 1. 安装项目

在你的项目中引用连接器项目：

```xml
<ItemGroup>
  <ProjectReference Include="..\src\QiaKon.Connector\QiaKon.Connector.csproj" />
  <ProjectReference Include="..\src\QiaKon.Connector.Http\QiaKon.Connector.Http.csproj" />
  <ProjectReference Include="..\src\QiaKon.Connector.Npgsql\QiaKon.Connector.Npgsql.csproj" />
</ItemGroup>
```

### 2. 配置连接器

在 `appsettings.json` 中定义连接器配置：

```json
{
  "Connectors": {
    "Connectors": [
      {
        "Name": "user-api",
        "Type": "Http",
        "Settings": {
          "BaseUrl": "https://api.example.com",
          "ConnectionTimeoutSeconds": 30,
          "MaxConnections": 100,
          "DefaultHeaders": {
            "Content-Type": "application/json"
          },
          "Endpoints": [
            {
              "Name": "get-user",
              "Method": "GET",
              "Url": "/users/{userId}",
              "Headers": {
                "Authorization": "Bearer {token}"
              },
              "ResponseDataPath": "data.user"
            },
            {
              "Name": "create-order",
              "Method": "POST",
              "Url": "/orders",
              "BodyTemplate": "{\"userId\":\"{userId}\",\"amount\":{amount}}",
              "ResponseDataPath": "data.orderId",
              "RetryCount": 3
            }
          ]
        }
      },
      {
        "Name": "main-db",
        "Type": "Npgsql",
        "Settings": {
          "ConnectionString": "Host=localhost;Database=mydb;Username=postgres;Password=secret",
          "MaxPoolSize": 100,
          "CommandTimeoutSeconds": 30,
          "QueryTemplates": [
            {
              "Name": "get-user-by-id",
              "SqlTemplate": "SELECT id, name, email FROM users WHERE id = @userId",
              "Parameters": [
                {
                  "Name": "@userId",
                  "DbType": "Integer",
                  "IsRequired": true
                }
              ],
              "ResultMapping": {
                "id": "Id",
                "name": "Name",
                "email": "Email"
              }
            },
            {
              "Name": "insert-order",
              "SqlTemplate": "INSERT INTO orders (user_id, amount, created_at) VALUES (@userId, @amount, NOW())",
              "CommandType": "Command",
              "Parameters": [
                {
                  "Name": "@userId",
                  "DbType": "Integer",
                  "IsRequired": true
                },
                {
                  "Name": "@amount",
                  "DbType": "Numeric",
                  "IsRequired": true
                }
              ]
            },
            {
              "Name": "get-recent-orders",
              "SqlTemplate": "SELECT * FROM orders WHERE user_id = @userId AND created_at > @since ORDER BY created_at DESC LIMIT @limit",
              "Parameters": [
                {
                  "Name": "@userId",
                  "DbType": "Integer",
                  "IsRequired": true
                },
                {
                  "Name": "@since",
                  "DbType": "Timestamp",
                  "IsRequired": true
                },
                {
                  "Name": "@limit",
                  "DbType": "Integer",
                  "IsRequired": false,
                  "DefaultValue": "10"
                }
              ],
              "EnableCache": true,
              "CacheTtlSeconds": 60
            }
          ]
        }
      }
    ]
  }
}
```

### 3. 注册服务

在 `Program.cs` 中注册连接器：

```csharp
using QiaKon.Connector;
using QiaKon.Connector.Http;
using QiaKon.Connector.Npgsql;

var builder = WebApplication.CreateBuilder(args);

// 注册连接器（从配置加载）
builder.Services.AddConnectors(
    builder.Configuration,
    config =>
    {
        // 解析每个连接器配置
        return config.Type.ToLowerInvariant() switch
        {
            "http" => HttpConnectorServiceCollectionExtensions.ParseFromConfiguration(config),
            "npgsql" => NpgsqlConnectorServiceCollectionExtensions.ParseFromConfiguration(config),
            _ => throw new NotSupportedException($"Connector type '{config.Type}' is not supported")
        };
    });

// 注册工厂
builder.Services.AddHttpConnectorSupport();
builder.Services.AddNpgsqlConnectorSupport();

var app = builder.Build();
app.Run();
```

### 4. 使用连接器

#### HTTP 调用示例

```csharp
public class UserService
{
    private readonly IHttpConnector _apiConnector;
    
    public UserService(IConnectorRegistry registry)
    {
        // 获取 HTTP 连接器
        _apiConnector = (IHttpConnector)registry.Get("user-api");
    }
    
    // 获取用户信息
    public async Task<UserDto?> GetUserAsync(string userId, string token)
    {
        var response = await _apiConnector.ExecuteAsync<UserDto>("get-user", new Dictionary<string, object>
        {
            ["userId"] = userId,
            ["token"] = token
        });
        
        return response;
    }
    
    // 创建订单
    public async Task<string> CreateOrderAsync(string userId, decimal amount)
    {
        var response = await _apiConnector.ExecuteAsync("create-order", new Dictionary<string, object>
        {
            ["userId"] = userId,
            ["amount"] = amount
        });
        
        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to create order: {response.ErrorMessage}");
        }
        
        return response.Data!.ToString()!;
    }
}

// DTO 类
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
```

#### 数据库查询示例

```csharp
public class OrderRepository
{
    private readonly IDbConnector _dbConnector;
    
    public OrderRepository(IConnectorRegistry registry)
    {
        // 获取数据库连接器
        _dbConnector = (IDbConnector)registry.Get("main-db");
    }
    
    // 查询单个用户
    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var users = await _dbConnector.QueryAsync<UserDto>("get-user-by-id", new Dictionary<string, object>
        {
            ["userId"] = userId
        });
        
        return users.FirstOrDefault();
    }
    
    // 查询列表
    public async Task<IReadOnlyList<OrderDto>> GetRecentOrdersAsync(int userId, DateTime since, int limit = 10)
    {
        return await _dbConnector.QueryAsync<OrderDto>("get-recent-orders", new Dictionary<string, object>
        {
            ["userId"] = userId,
            ["since"] = since,
            ["limit"] = limit
        });
    }
    
    // 执行 INSERT/UPDATE/DELETE
    public async Task<int> CreateOrderAsync(int userId, decimal amount)
    {
        return await _dbConnector.ExecuteCommandAsync("insert-order", new Dictionary<string, object>
        {
            ["userId"] = userId,
            ["amount"] = amount
        });
    }
}

// DTO 类
public class OrderDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## 配置详解

### HTTP 端点配置 (HttpEndpointConfig)

| 字段                 | 类型       | 必填 | 说明                                     |
| -------------------- | ---------- | ---- | ---------------------------------------- |
| `Name`               | string     | ✅    | 端点名称（唯一标识）                     |
| `Method`             | string     | ✅    | HTTP 方法（GET/POST/PUT/DELETE/PATCH）   |
| `Url`                | string     | ✅    | 请求 URL（支持 `{param}` 模板变量）      |
| `Headers`            | Dictionary | ❌    | 请求头（支持模板变量）                   |
| `BodyTemplate`       | string     | ❌    | 请求体 JSON 模板（支持模板变量）         |
| `QueryParameters`    | Dictionary | ❌    | 查询参数模板                             |
| `ResponseDataPath`   | string     | ❌    | JSONPath，用于提取嵌套响应数据           |
| `SuccessStatusCodes` | string     | ❌    | 成功状态码（默认 200-299，如 "200,201"） |
| `TimeoutSeconds`     | int        | ❌    | 超时时间（秒）                           |
| `RetryCount`         | int        | ❌    | 重试次数（默认 0）                       |

### 数据库查询模板配置 (DbQueryTemplateConfig)

| 字段              | 类型       | 必填 | 说明                                                  |
| ----------------- | ---------- | ---- | ----------------------------------------------------- |
| `Name`            | string     | ✅    | 模板名称（唯一标识）                                  |
| `SqlTemplate`     | string     | ✅    | SQL 模板（支持 `@param` 参数）                        |
| `CommandType`     | string     | ❌    | 查询类型（Query/Command/StoredProcedure，默认 Query） |
| `Parameters`      | List       | ❌    | 参数定义列表                                          |
| `ResultMapping`   | Dictionary | ❌    | 结果列映射（数据库列名 → 属性名）                     |
| `TimeoutSeconds`  | int        | ❌    | 超时时间（秒）                                        |
| `EnableCache`     | bool       | ❌    | 是否启用缓存（默认 false）                            |
| `CacheTtlSeconds` | int        | ❌    | 缓存 TTL（秒，默认 60）                               |

### 数据库参数配置 (DbParameterConfig)

| 字段           | 类型   | 必填 | 说明                                                                        |
| -------------- | ------ | ---- | --------------------------------------------------------------------------- |
| `Name`         | string | ✅    | 参数名称（对应 SQL 中的 `@param`）                                          |
| `DbType`       | string | ❌    | 参数类型（Integer/Text/Timestamp/Numeric/Boolean/Uuid/Jsonb 等，默认 Text） |
| `IsRequired`   | bool   | ❌    | 是否必填（默认 false）                                                      |
| `DefaultValue` | string | ❌    | 默认值                                                                      |

## 模板变量用法

### HTTP URL 模板

```json
{
  "Url": "/users/{userId}/orders/{orderId}",
  "Headers": {
    "Authorization": "Bearer {token}"
  }
}
```

调用时传入参数：

```csharp
await connector.ExecuteAsync("get-order", new Dictionary<string, object>
{
    ["userId"] = "123",
    ["orderId"] = "456",
    ["token"] = "eyJhbGciOi..."
});
```

### HTTP Body 模板

```json
{
  "BodyTemplate": "{\"userId\":\"{userId}\",\"items\":[{\"id\":\"{itemId}\",\"quantity\":{quantity}}]}"
}
```

### SQL 参数模板

```json
{
  "SqlTemplate": "SELECT * FROM orders WHERE user_id = @userId AND status = @status",
  "Parameters": [
    { "Name": "@userId", "DbType": "Integer", "IsRequired": true },
    { "Name": "@status", "DbType": "Text", "DefaultValue": "pending" }
  ]
}
```

## 响应数据提取

### JSONPath 提取

配置 `ResponseDataPath` 可以从嵌套的 JSON 响应中提取数据：

```json
{
  "ResponseDataPath": "data.user.profile"
}
```

响应示例：

```json
{
  "code": 200,
  "data": {
    "user": {
      "profile": {
        "id": "123",
        "name": "John"
      }
    }
  }
}
```

提取结果：

```json
{
  "id": "123",
  "name": "John"
}
```

## 扩展连接器

### 添加新的连接器类型

1. 创建新项目 `QiaKon.Connector.Redis`

2. 实现接口：

```csharp
public sealed class RedisConnectorOptions : IConnectorOptions
{
    public string Name { get; set; } = string.Empty;
    public ConnectorType Type => ConnectorType.Redis;
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class RedisConnector : ConnectorBase, IConnector
{
    public RedisConnector(RedisConnectorOptions options)
        : base(options.Name, ConnectorType.Redis) { }
    
    // 实现抽象方法...
}

public sealed class RedisConnectorFactory : IConnectorFactory
{
    public IConnector Create(IConnectorOptions options)
    {
        if (options is not RedisConnectorOptions redisOptions)
            throw new ArgumentException("Expected RedisConnectorOptions");
        
        return new RedisConnector(redisOptions);
    }
}
```

3. 注册到 DI：

```csharp
builder.Services.AddSingleton<IConnectorFactory, RedisConnectorFactory>();
```

## 最佳实践

### 1. 配置管理

- 将敏感信息（连接字符串、API Key）使用环境变量或密钥管理
- 不同环境使用不同的 `appsettings.{Environment}.json`

```json
// appsettings.Development.json
{
  "Connectors": {
    "Connectors": [
      {
        "Name": "main-db",
        "Settings": {
          "ConnectionString": "Host=localhost;Database=devdb"
        }
      }
    ]
  }
}
```

### 2. 错误处理

```csharp
try
{
    var response = await connector.ExecuteAsync("get-user", parameters);
    
    if (!response.IsSuccess)
    {
        _logger.LogError("API call failed: {Error}", response.ErrorMessage);
        return null;
    }
    
    return response.Data as UserDto;
}
catch (KeyNotFoundException ex)
{
    _logger.LogError("Endpoint not found: {Error}", ex.Message);
    throw;
}
catch (ConnectorException ex)
{
    _logger.LogError("Connector error: {Error}", ex.Message);
    throw;
}
```

### 3. 性能优化

- 启用数据库查询缓存（`EnableCache: true`）
- 合理设置连接池大小（`MaxPoolSize`）
- 使用 `ResponseDataPath` 减少不必要的数据传输
- 为高频查询设置合理的超时时间

### 4. 日志和监控

```csharp
public class ConnectorLoggingDecorator : IHttpConnector
{
    private readonly IHttpConnector _inner;
    private readonly ILogger<ConnectorLoggingDecorator> _logger;
    
    public async Task<ConnectorResponse> ExecuteAsync(
        string endpointName,
        IDictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await _inner.ExecuteAsync(endpointName, parameters, cancellationToken);
            
            _logger.LogInformation(
                "HTTP endpoint {Endpoint} executed in {Elapsed}ms, success: {Success}",
                endpointName, stopwatch.ElapsedMilliseconds, response.IsSuccess);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP endpoint {Endpoint} failed", endpointName);
            throw;
        }
    }
}
```

## 项目结构

```
QiaKon.Connector/
├── IConnector.cs                       # 连接器接口定义
├── ConnectorOptions.cs                 # 配置选项模型
├── ConnectorBase.cs                    # 连接器抽象基类
├── ConnectorManager.cs                 # 配置驱动管理器
├── ConnectorRegistry.cs                # 连接器注册表
├── ConnectorHostedService.cs           # 宿主服务
├── ConnectorServiceCollectionExtensions.cs  # DI 扩展
└── README.md                           # 本文档

QiaKon.Connector.Http/
├── HttpConnector.cs                    # HTTP 连接器实现
├── HttpConnectorOptions.cs             # HTTP 配置选项
├── HttpConnectorFactory.cs             # HTTP 工厂
└── HttpConnectorServiceCollectionExtensions.cs

QiaKon.Connector.Npgsql/
├── NpgsqlConnector.cs                  # PostgreSQL 连接器实现
├── NpgsqlConnectorOptions.cs           # PostgreSQL 配置选项
├── NpgsqlConnectorFactory.cs           # PostgreSQL 工厂
└── NpgsqlConnectorServiceCollectionExtensions.cs
```

## 依赖项

- .NET 10.0
- `Microsoft.Extensions.Hosting.Abstractions`
- `Microsoft.Extensions.Configuration.Binder`
- `System.Net.Http` (HTTP 连接器)
- `Npgsql` (PostgreSQL 连接器)

## 常见问题

### Q: 如何调试连接器？

A: 启用详细日志记录，检查配置是否正确加载：

```csharp
var registry = app.Services.GetRequiredService<IConnectorRegistry>();
foreach (var name in registry.GetAllNames())
{
    var connector = registry.Get(name);
    Console.WriteLine($"Connector: {name}, Type: {connector.Type}, State: {connector.State}");
}
```

### Q: 如何处理动态参数？

A: 在调用时传入任意参数字典，连接器会自动替换模板变量：

```csharp
await connector.ExecuteAsync("search", new Dictionary<string, object>
{
    ["keyword"] = "test",
    ["page"] = 1,
    ["pageSize"] = 20,
    ["filters"] = new { status = "active" }
});
```

### Q: 如何实现连接器的重试机制？

A: 在端点配置中设置 `RetryCount`：

```json
{
  "Name": "external-api",
  "RetryCount": 3
}
```

## 许可证

MIT