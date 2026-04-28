# QiaKon.Cache.Hybrid - AGENTS.md

> **模块**: 多级缓存层  
> **职责**: 提供 L1/L2/L3 多级缓存实现  
> **依赖**: `QiaKon.Contracts`  
> **被依赖**: `QiaKon.Api`, 所有需要缓存的业务模块

---

## 一、模块职责

本模块实现多级缓存体系，加速热数据访问，降低后端存储压力。

**核心职责**:
- L1: `MemoryCache` - 进程内缓存（最高速）
- L2: `HybridCache` - 混合缓存（Memory + 分布式）
- L3: `RedisCache` - 分布式缓存（最大容量）
- 实现缓存策略（Cache-Aside, Read-Through, Write-Through, Write-Behind）

---

## 二、核心接口

### 2.1 缓存接口

```csharp
public interface ICache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
```

### 2.2 缓存配置

```csharp
public class CacheEntryOptions
{
    public TimeSpan? AbsoluteExpiration { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public DateTimeOffset? AbsoluteExpirationRelativeToNow { get; set; }
    public string? Region { get; set; }
}
```

---

## 三、缓存实现

### 3.1 MemoryCache (L1)

| 特性         | 说明                         |
| ------------ | ---------------------------- |
| **速度**     | 最高速（内存访问）           |
| **容量**     | 低（进程内存限制）           |
| **作用域**   | 单进程                       |
| **适用场景** | 热点数据、会话数据、配置数据 |

**配置示例**:
```json
{
  "MemoryCache": {
    "SizeLimit": 1024,
    "ExpirationScanFrequency": "00:01:00"
  }
}
```

### 3.2 HybridCache (L2)

| 特性         | 说明                    |
| ------------ | ----------------------- |
| **速度**     | 中等（Memory + 分布式） |
| **容量**     | 中                      |
| **作用域**   | 多进程                  |
| **适用场景** | 业务数据、查询结果      |

**策略**:
- Read-Through: 缓存未命中自动从源加载
- Write-Through: 写入时同步更新缓存

### 3.3 RedisCache (L3)

| 特性         | 说明                     |
| ------------ | ------------------------ |
| **速度**     | 较慢（网络延迟）         |
| **容量**     | 最大                     |
| **作用域**   | 分布式                   |
| **适用场景** | 全局共享数据、跨服务数据 |

**配置示例**:
```json
{
  "RedisCache": {
    "ConnectionString": "localhost:6379",
    "Database": 0,
    "KeyPrefix": "qiakon:"
  }
}
```

---

## 四、缓存策略

### 4.1 Cache-Aside (默认)

```csharp
public async Task<T?> GetOrSetAsync<T>(
    string key, 
    Func<Task<T?>> factory, 
    CacheEntryOptions? options = null)
{
    var cached = await GetAsync<T>(key);
    if (cached != null) return cached;
    
    var value = await factory();
    if (value != null)
    {
        await SetAsync(key, value, options);
    }
    return value;
}
```

### 4.2 Read-Through

缓存未命中时，自动调用数据源加载函数。

### 4.3 Write-Through

写入缓存时，同步写入数据源。

### 4.4 Write-Behind

写入缓存后，异步批量写入数据源（提升写入性能）。

---

## 五、开发规范

### 5.1 缓存键命名规范

格式: `{模块}:{实体类型}:{操作}:{标识}`

示例:
- `user:profile:get:{userId}`
- `document:chunks:list:{documentId}`
- `graph:entity:get:{entityId}`

### 5.2 过期策略

| 数据类型 | 策略     | 推荐时间  |
| -------- | -------- | --------- |
| 配置数据 | 绝对过期 | 1 小时    |
| 会话数据 | 滑动过期 | 30 分钟   |
| 查询结果 | 滑动过期 | 5-15 分钟 |
| 热点数据 | 绝对过期 | 1-5 分钟  |

### 5.3 缓存穿透防护

- 空值缓存：查询结果为空时，缓存空值对象（短时间）
- 布隆过滤器：大量无效 key 时，使用布隆过滤器预过滤

### 5.4 缓存雪崩防护

- 随机过期时间：在基础过期时间上增加随机偏移
- 互斥锁：缓存失效时，使用分布式锁防止并发重建

### 5.5 缓存击穿防护

- 热点数据永不过期：后台定时刷新
- 互斥锁：单个 key 失效时，仅允许一个线程重建

---

## 六、注册与配置

### 6.1 DI 注册

```csharp
// 注册 L1 MemoryCache
services.AddMemoryCache();

// 注册 L2 HybridCache
services.AddHybridCache(options =>
{
    options.L1SizeLimit = 1024;
    options.L1Expiration = TimeSpan.FromMinutes(5);
});

// 注册 L3 RedisCache
services.AddRedisCache(options =>
{
    options.ConnectionString = configuration["Redis:ConnectionString"];
    options.KeyPrefix = "qiakon:";
});
```

### 6.2 缓存提供者选择

```csharp
// 根据场景选择缓存层级
services.AddScoped<ICache>(sp => 
{
    var context = sp.GetRequiredService<IHttpContextAccessor>();
    return context.HttpContext.Request.Query["cacheLevel"] switch
    {
        "L1" => sp.GetRequiredService<IMemoryCacheService>(),
        "L2" => sp.GetRequiredService<IHybridCacheService>(),
        "L3" => sp.GetRequiredService<IRedisCacheService>(),
        _ => sp.GetRequiredService<IHybridCacheService>() // 默认 L2
    };
});
```

---

## 七、监控与诊断

### 7.1 缓存指标

- 命中率（Hit Rate）
- 未命中率（Miss Rate）
- 平均响应时间
- 内存使用量

### 7.2 健康检查

```csharp
public class CacheHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        // 检查缓存连接状态
        // 检查内存使用
        // 返回健康状态
    }
}
```

---

## 八、测试要求

### 8.1 单元测试

- 缓存读写逻辑
- 过期策略验证
- 并发安全测试

### 8.2 集成测试

- Redis 连接测试（使用 Testcontainers）
- 多级缓存协同测试
- 缓存策略测试

---

## 九、注意事项

1. **序列化**: 使用 `System.Text.Json` 序列化，确保类型兼容
2. **内存泄漏**: 设置合理的 SizeLimit 和过期策略
3. **分布式锁**: 使用 RedLock 算法实现分布式锁
4. **缓存一致性**: 数据更新时，及时失效相关缓存

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
