# QiaKon.EntityFrameworkCore.Npgsql

PostgreSQL 特定的 EF Core 扩展，提供数据库优化、健康检查和性能监控功能。

## 功能特性

### 1. PostgreSQL 优化配置
- UUID 类型自动映射
- 连接池优化
- 重试机制
- 查询分割行为

### 2. 数据库健康检查
- 连接可用性检测
- 数据库大小查询
- 表大小统计
- 索引使用情况分析

### 3. 性能优化
- VACUUM ANALYZE 支持
- 批量操作优化
- 连接池默认配置

## 快速开始

### 1. 创建 DbContext

```csharp
public class AppDbContext : QiaKonNpgsqlDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options)
    {
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options, 
        IAuditContextProvider auditContextProvider) 
        : base(options, auditContextProvider)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
}
```

### 2. 注册服务

```csharp
// Program.cs

// 方式 1：使用默认审计提供者（基于 HttpContext）
builder.Services.AddQiaKonNpgsqlDbContext<AppDbContext>(
    builder.Configuration.GetConnectionString("Default"));

// 方式 2：使用自定义审计提供者
builder.Services.AddQiaKonNpgsqlDbContext<AppDbContext, CustomAuditProvider>(
    builder.Configuration.GetConnectionString("Default"));

// 方式 3：自定义 Npgsql 选项
builder.Services.AddQiaKonNpgsqlDbContext<AppDbContext>(
    builder.Configuration.GetConnectionString("Default"),
    npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly("Your.Migrations.Assembly");
        npgsqlOptions.CommandTimeout(60);
    });
```

### 3. 连接字符串配置

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=qiaKon;Username=postgres;Password=your_password;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Idle Lifetime=300"
  }
}
```

## 数据库监控

### 检查数据库连接

```csharp
var isAvailable = await _context.IsDatabaseAvailableAsync();
if (!isAvailable)
{
    // 数据库不可用，执行降级逻辑
}
```

### 获取数据库大小

```csharp
var sizeInMb = await _context.GetDatabaseSizeInMbAsync();
Console.WriteLine($"数据库大小：{sizeInMb:F2} MB");
```

### 获取表大小信息

```csharp
var tableSizes = await _context.GetTableSizesAsync();
foreach (var table in tableSizes)
{
    Console.WriteLine($"表：{table.TableName}");
    Console.WriteLine($"  总大小：{table.TotalSize}");
    Console.WriteLine($"  表大小：{table.TableSize}");
    Console.WriteLine($"  索引大小：{table.IndexSize}");
}
```

### 获取索引使用情况

```csharp
var indexUsage = await _context.GetIndexUsageAsync();
foreach (var index in indexUsage)
{
    Console.WriteLine($"表：{index.TableName}");
    Console.WriteLine($"  索引：{index.IndexName}");
    Console.WriteLine($"  扫描次数：{index.Scans}");
    Console.WriteLine($"  读取行数：{index.TupRead}");
}
```

### 执行 VACUUM ANALYZE

```csharp
// 优化单个表
await _context.VacuumAnalyzeAsync("users");

// 优化所有用户表（需要循环调用）
var tableSizes = await _context.GetTableSizesAsync();
foreach (var table in tableSizes)
{
    await _context.VacuumAnalyzeAsync(table.TableName);
}
```

## 性能优化建议

### 1. 连接池配置

推荐连接字符串参数：
- `Pooling=true` - 启用连接池
- `Min Pool Size=5` - 最小连接数
- `Max Pool Size=100` - 最大连接数
- `Connection Idle Lifetime=300` - 空闲连接生存时间（秒）
- `Connection Pruning Interval=60` - 连接清理间隔（秒）

### 2. 批量操作

使用 EF Core 的批量操作：

```csharp
// 批量插入
var entities = new List<User> { /* ... */ };
await _context.BulkInsertAsync(entities);

// 批量更新
await _context.BulkUpdateAsync(entities);

// 批量删除
await _context.BulkDeleteAsync(entities);
```

### 3. 查询分割

默认启用查询分割行为，复杂查询会自动拆分为多个 SQL 查询，避免笛卡尔积爆炸：

```csharp
// 自动使用 SplitQuery
var orders = await _context.Orders
    .Include(o => o.Items)
    .Include(o => o.User)
    .ToListAsync();
```

### 4. 重试机制

默认启用重试机制（最多 3 次，间隔 5 秒），适用于处理瞬态故障：

```csharp
// 自动重试以下异常：
// - 连接超时
// - 执行超时
// - 网络中断
// - 死锁
```

## 数据库维护

### 定期清理建议

```csharp
// 每天执行一次 VACUUM ANALYZE
public class DatabaseMaintenanceService : BackgroundService
{
    private readonly IServiceProvider _services;

    public DatabaseMaintenanceService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);

            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tables = await context.GetTableSizesAsync(stoppingToken);
            foreach (var table in tables)
            {
                await context.VacuumAnalyzeAsync(table.TableName, stoppingToken);
            }
        }
    }
}
```

## 注意事项

1. **UUID 主键**：所有 `Guid` 类型的 `Id` 字段会自动映射为 PostgreSQL 的 `uuid` 类型
2. **时区处理**：PostgreSQL 的 `timestamp with time zone` 会自动处理时区转换
3. **JSON 支持**：可以使用 `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` 支持 JSONB
4. **全文搜索**：PostgreSQL 内置全文搜索功能，可通过 `EF.Functions.ToTsVector` 使用

## 与 QiaKon.EntityFrameworkCore 的关系

`QiaKon.EntityFrameworkCore.Npgsql` 继承自 `QiaKon.EntityFrameworkCore`，包含其所有功能：
- 审计字段自动填充
- 软删除支持
- 乐观并发控制
- 批量操作方法

在此基础上增加了 PostgreSQL 特定的优化和监控功能。
