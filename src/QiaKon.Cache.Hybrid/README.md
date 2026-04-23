# QiaKon.Cache.Hybrid

多级混合缓存实现，结合 **L1 内存缓存** 和 **L2 Redis 缓存**，通过 **版本号机制** 和 **Redis Pub/Sub** 保证分布式环境下的数据一致性。

## 设计思路

### 架构概览

```
┌─────────────────────────────────────────────────────────┐
│                      应用层                              │
│                   调用 ICache 接口                       │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│                HybridCache（多级缓存）                    │
│                                                         │
│  ┌─────────────────┐         ┌──────────────────────┐  │
│  │   L1: 内存缓存   │ ◄─────► │   版本号追踪          │  │
│  │  (快速访问)      │         │   (ConcurrentDict)   │  │
│  └─────────────────┘         └──────────┬───────────┘  │
│           │                              │              │
│           ▼                              ▼              │
│  ┌─────────────────┐         ┌──────────────────────┐  │
│  │   L2: Redis 缓存 │ ◄─────► │   Pub/Sub 失效通知    │  │
│  │  (分布式共享)    │         │   (跨节点同步)        │  │
│  └─────────────────┘         └──────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│                    数据源（数据库等）                      │
└─────────────────────────────────────────────────────────┘
```

### 核心问题

多级缓存面临的关键挑战是 **数据一致性**：

- 当 L2（Redis）中的数据过期或被更新时，L1（内存）可能仍持有旧数据
- 在多节点部署中，某个节点更新了缓存，其他节点的 L1 如何感知？

### 解决方案

#### 1. 版本号机制（Version Tracking）

每次写入缓存时，生成递增的版本号并存储到 Redis：

```
Key: user:123          →  Value: {User 对象}
Key: qiaKon:cache:version:user:123  →  Value: 42 (版本号)
```

**读取流程**：

1. 从 L1 获取数据和本地记录的版本号
2. 从 L2 获取当前最新版本号
3. 如果 `L2 版本号 > 本地版本号`，说明 L1 数据已过期，删除 L1 并从 L2 读取
4. 如果版本号一致，直接返回 L1 数据

**写入流程**：

1. 递增版本号
2. 并行写入 L2 数据和版本号到 Redis
3. 发布 Pub/Sub 失效通知
4. 更新本地版本号并写入 L1

#### 2. Redis Pub/Sub 失效通知

利用 Redis 的发布订阅功能，实现跨节点的缓存失效同步：

```
节点 A 更新缓存
    │
    ├─ 写入 L2 + 版本号
    │
    ├─ 发布失效消息到通道: "qiaKon:cache:invalidation"
    │
    ▼
┌─────────────────────────────┐
│   Redis Pub/Sub 通道         │
│   "qiaKon:cache:invalidation"│
└──────────┬──────────────────┘
           │
    ┌──────┴──────┐
    ▼             ▼
  节点 B         节点 C
    │             │
    ├─ 收到通知    ├─ 收到通知
    ├─ 删除本地L1  ├─ 删除本地L1
    └─ 清理版本号  └─ 清理版本号
```

**优势**：

- 毫秒级失效传播
- 无需轮询，事件驱动
- 支持全局清空通知（`*:clear`）

#### 3. 读写策略

**读取策略（Read-Through）**：

```
L1 命中 → 版本校验 → 返回数据
   ↓ (未命中/过期)
L2 命中 → 回填 L1 → 返回数据
   ↓ (未命中)
查询数据源 → 写入 L1+L2 → 返回数据
```

**写入策略（Write-Through）**：

```
写入请求
   │
   ├─ 递增版本号
   │
   ├─ 并行写入 L2 + 版本号到 Redis
   │
   ├─ 发布 Pub/Sub 失效通知
   │
   ├─ 更新本地版本号
   │
   └─ 写入 L1
```

### 关键配置

| 配置项                | 默认值                      | 说明                                |
| --------------------- | --------------------------- | ----------------------------------- |
| `L1Ttl`               | 3 分钟                      | L1 缓存 TTL，建议为 L2 的 1/6 ~ 1/3 |
| `L2Ttl`               | 30 分钟                     | L2 缓存 TTL                         |
| `EnableVersionCheck`  | true                        | 是否启用版本校验                    |
| `EnableL1Backfill`    | true                        | L2 命中时是否回填 L1                |
| `InvalidationChannel` | `qiaKon:cache:invalidation` | Pub/Sub 通道名称                    |
| `VersionPrefix`       | `qiaKon:cache:version:`     | 版本号存储前缀                      |

## 使用方式

### 1. 安装和注册

在 `Program.cs` 中注册服务：

```csharp
using QiaKon.Cache;
using QiaKon.Cache.Hybrid;

var builder = WebApplication.CreateBuilder(args);

// 注册多级缓存
builder.Services.AddHybridCache(
    redisConfiguration: builder.Configuration.GetConnectionString("Redis"),
    configureOptions: options =>
    {
        options.L1Ttl = TimeSpan.FromMinutes(3);
        options.L2Ttl = TimeSpan.FromMinutes(30);
        options.EnableVersionCheck = true;
        options.EnableL1Backfill = true;
    });

var app = builder.Build();
app.Run();
```

### 2. 基础使用

通过依赖注入获取 `ICache` 接口：

```csharp
public class UserService
{
    private readonly ICache _cache;
    private readonly AppDbContext _dbContext;

    public UserService(ICache cache, AppDbContext dbContext)
    {
        _cache = cache;
        _dbContext = dbContext;
    }

    // 获取或创建缓存
    public async Task<User?> GetUserAsync(int userId, CancellationToken ct = default)
    {
        var key = $"user:{userId}";

        return await _cache.GetOrCreateAsync(
            key,
            async () => await _dbContext.Users.FindAsync(userId),
            CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromHours(1)),
            ct
        );
    }

    // 更新缓存
    public async Task UpdateUserAsync(User user, CancellationToken ct = default)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(ct);

        // 更新缓存（自动处理版本号和 Pub/Sub 通知）
        var key = $"user:{user.Id}";
        await _cache.SetAsync(
            key,
            user,
            CacheEntryOptions.WithAbsoluteExpiration(TimeSpan.FromHours(1)),
            ct
        );
    }

    // 删除缓存
    public async Task DeleteUserAsync(int userId, CancellationToken ct = default)
    {
        var key = $"user:{userId}";
        await _cache.RemoveAsync(key, ct);
    }
}
```

### 3. 批量操作

```csharp
public class ProductCacheService
{
    private readonly ICache _cache;

    public ProductCacheService(ICache cache) => _cache = cache;

    // 批量获取
    public async Task<Dictionary<string, Product>> GetProductsAsync(
        IEnumerable<int> productIds,
        CancellationToken ct = default)
    {
        var keys = productIds.Select(id => $"product:{id}");
        return await _cache.GetManyAsync<Product>(keys, ct);
    }

    // 批量设置
    public async Task CacheProductsAsync(
        Dictionary<string, Product> products,
        CancellationToken ct = default)
    {
        await _cache.SetManyAsync(
            products,
            CacheEntryOptions.WithSlidingExpiration(TimeSpan.FromMinutes(20)),
            ct
        );
    }
}
```

### 4. 自定义配置

```csharp
// 使用不同的 Redis 数据库
builder.Services.AddHybridCache(
    redisConfiguration: "localhost:6379,password=yourpassword",
    configureOptions: options =>
    {
        options.Database = 1; // 使用 Redis database 1

        // 自定义 L1 选项
        options.L1Options = new CacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(2),
            Priority = CacheItemPriority.High
        };

        // 自定义 L2 选项
        options.L2Options = CacheEntryOptions.WithAbsoluteExpiration(
            TimeSpan.FromHours(2)
        );
    });
```

### 5. 高级配置

```csharp
// 使用 HybridCacheRegistrationOptions 进行更精细的控制
builder.Services.AddHybridCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.Database = 0;

    options.HybridCacheOptions.L1Ttl = TimeSpan.FromMinutes(1);
    options.HybridCacheOptions.L2Ttl = TimeSpan.FromHours(1);
    options.HybridCacheOptions.InvalidationChannel = "myapp:cache:invalidation";
    options.HybridCacheOptions.VersionPrefix = "myapp:version:";
});
```

## 一致性保证

### 单节点场景

| 操作 | L1          | L2      | 版本号  |
| ---- | ----------- | ------- | ------- |
| 写入 | ✅ 更新     | ✅ 更新 | ✅ 递增 |
| 读取 | ✅ 校验版本 | ✅ 回填 | ✅ 检查 |
| 删除 | ✅ 删除     | ✅ 删除 | ✅ 清理 |

### 多节点场景

| 操作       | 节点 A        | 节点 B                      | 节点 C                      |
| ---------- | ------------- | --------------------------- | --------------------------- |
| A 写入缓存 | ✅ 更新 L1+L2 | 📡 收到通知 → 删除 L1       | 📡 收到通知 → 删除 L1       |
| B 读取缓存 | -             | ❌ L1 未命中 → 读 L2 → 回填 | ❌ L1 未命中 → 读 L2 → 回填 |

### 失效场景示例

**场景**：节点 A 更新了 `user:123`，节点 B 的 L1 仍持有旧数据

```
时间线：
T0: 节点 B 读取 user:123，L1 命中，本地版本 = 42
T1: 节点 A 更新 user:123，版本号递增到 43
T2: 节点 A 发布失效通知到 Pub/Sub
T3: 节点 B 收到通知，删除 L1 中的 user:123
T4: 节点 B 再次读取 user:123，L1 未命中 → 从 L2 读取（版本 43）
```

即使 Pub/Sub 通知延迟到达，版本校验机制也会保证：

```
节点 B 读取 user:123:
  1. L1 命中，数据版本 = 42
  2. 查询 L2 版本号 = 43
  3. 43 > 42 → L1 数据过期，删除
  4. 从 L2 读取最新数据
```

## 性能优化建议

1. **L1 TTL 设置**：建议为 L2 TTL 的 1/6 ~ 1/3，例如 L2=30min，L1=3-5min
2. **热点数据预热**：应用启动时预加载热点数据到 L1
3. **避免大对象**：L1 占用进程内存，缓存大对象可能导致 GC 压力
4. **监控版本号增长**：高频写入场景下版本号增长快，注意 Redis 内存使用
5. **合理选择缓存键**：使用有意义的键前缀，便于管理和清理

## 故障降级

当 Redis 不可用时，可以降级为纯 L1 模式：

```csharp
// 检测 Redis 连接状态
if (!redis.IsConnected)
{
    // 降级为内存缓存
    services.AddSingleton<ICache, MemoryCache>();
}
else
{
    services.AddHybridCache(...);
}
```

## 与 ASP.NET Core IDistributedCache 的对比

| 特性         | HybridCache         | IDistributedCache |
| ------------ | ------------------- | ----------------- |
| L1 内存缓存  | ✅ 内置             | ❌ 需要额外实现   |
| 分布式一致性 | ✅ 版本号 + Pub/Sub | ❌ 无             |
| 批量操作     | ✅ GetMany/SetMany  | ❌ 仅单个操作     |
| 版本追踪     | ✅ 自动管理         | ❌ 无             |
| 失效通知     | ✅ 跨节点同步       | ❌ 无             |

## 注意事项

1. **Redis 依赖**：必须确保 Redis 可用，否则 Pub/Sub 失效通知无法工作
2. **网络延迟**：Pub/Sub 通知依赖网络，极端情况下可能有毫秒级延迟
3. **内存使用**：L1 缓存占用进程内存，需监控内存使用情况
4. **版本号存储**：每个缓存键会额外存储一个版本号到 Redis
5. **线程安全**：`HybridCache` 是线程安全的，可在多线程环境中使用

## 项目结构

```
QiaKon.Cache.Hybrid/
├── HybridCache.cs                      # 多级缓存核心实现
├── HybridCacheOptions.cs               # 缓存配置选项
├── HybridCacheServiceCollectionExtensions.cs  # DI 注册扩展
├── QiaKon.Cache.Hybrid.csproj          # 项目文件
└── README.mdx                          # 本文档
```

## 依赖项

- `QiaKon.Cache` - 缓存抽象接口
- `QiaKon.Cache.Memory` - L1 内存缓存实现
- `QiaKon.Cache.Redis` - L2 Redis 缓存实现
- `StackExchange.Redis` - Redis 客户端库
- `Microsoft.Extensions.Caching.Memory` - .NET 内存缓存
- `Microsoft.Extensions.DependencyInjection.Abstractions` - DI 抽象
