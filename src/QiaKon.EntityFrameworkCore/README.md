# QiaKon.EntityFrameworkCore

标准化的 EF Core 基础库，提供审计字段处理、软删除、乐观并发等企业级功能。

## 功能特性

### 1. 自动审计字段填充
- `CreatedBy` / `CreatedAt` - 创建时自动填充
- `ModifiedBy` / `ModifiedAt` - 修改时自动填充

### 2. 软删除支持
- 全局查询过滤器自动过滤已删除数据
- 提供 `SoftDelete()` 方法标记删除
- 支持查询已删除数据（`WithDeleted`、`OnlyDeleted`）

### 3. 乐观并发控制
- 通过 `RowVersion` 字段实现并发控制
- 自动检测并发冲突

### 4. 常用扩展方法
- 批量插入/更新/删除
- 分页查询
- 事务管理

## 快速开始

### 1. 定义实体

```csharp
// 基础实体（仅 ID）
public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 审计实体（包含创建/修改信息）
public class Order : AuditableEntityBase
{
    public string OrderNo { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

// 软删除实体
public class User : SoftDeleteEntityBase
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// 版本控制实体（乐观并发）
public class Inventory : EntityBase, IVersionable
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

### 2. 创建 DbContext

```csharp
public class AppDbContext : QiaKonDbContext
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

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<User> Users => Set<User>();
}
```

### 3. 注册服务

```csharp
// Program.cs
builder.Services.AddQiaKonAuditContext(); // 注册审计提供者

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
    // 或使用 UseNpgsql、UseMySql 等
});
```

## 使用示例

### 软删除

```csharp
// 删除（软删除）
var user = await _context.Users.FindAsync(userId);
_context.SoftDelete(user);
await _context.SaveChangesAsync();

// 批量软删除
var oldUsers = await _context.Users
    .Where(u => u.CreatedAt < DateTime.UtcNow.AddYears(-1))
    .ToListAsync();
await _context.SoftDeleteRangeAsync(oldUsers);

// 查询包含已删除的数据
var allUsers = await _context.WithDeleted<User>().ToListAsync();

// 仅查询已删除的数据
var deletedUsers = await _context.OnlyDeleted<User>().ToListAsync();
```

### 审计字段

审计字段会自动填充，无需手动设置：

```csharp
var order = new Order 
{ 
    OrderNo = "ORD-001", 
    TotalAmount = 100 
};

_context.Orders.Add(order);
await _context.SaveChangesAsync();

// 自动填充：
// order.CreatedBy = 当前用户 ID
// order.CreatedAt = 当前时间
// order.ModifiedBy = 当前用户 ID
// order.ModifiedAt = 当前时间
```

### 批量操作

```csharp
// 批量插入
var products = new List<Product> 
{ 
    new() { Name = "Product 1", Price = 10 },
    new() { Name = "Product 2", Price = 20 }
};
await _context.BulkInsertAsync(products);

// 批量更新
foreach (var product in products)
{
    product.Price *= 1.1m; // 涨价 10%
}
await _context.BulkUpdateAsync(products);

// 批量删除
await _context.BulkDeleteAsync(products);
```

### 事务管理

```csharp
var result = await _context.ExecuteInTransactionAsync(async () =>
{
    var order = new Order { OrderNo = "ORD-002", TotalAmount = 200 };
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();

    var inventory = new Inventory { ProductId = order.Id, Quantity = 100 };
    _context.Inventories.Add(inventory);
    await _context.SaveChangesAsync();

    return order.Id;
});
```

### 分页查询

```csharp
var query = _context.Products
    .Where(p => p.Price > 100)
    .OrderBy(p => p.Name);

var (items, totalCount) = await query.GetPagedAsync(
    pageNumber: 1, 
    pageSize: 20);
```

## 自定义审计提供者

如果需要从其他来源获取用户信息（如消息队列、后台任务），可以实现自定义审计提供者：

```csharp
public class CustomAuditProvider : IAuditContextProvider
{
    private readonly ICurrentUserService _currentUserService;

    public CustomAuditProvider(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Guid GetCurrentUserId()
    {
        return _currentUserService.GetCurrentUserId();
    }

    public string? GetCurrentUserName()
    {
        return _currentUserService.GetCurrentUserName();
    }
}

// 注册
builder.Services.AddQiaKonAuditContext<CustomAuditProvider>();
```

## 实体基类说明

| 基类                   | 包含字段                                                   | 适用场景             |
| ---------------------- | ---------------------------------------------------------- | -------------------- |
| `EntityBase`           | `Id`                                                       | 简单实体，不需要审计 |
| `AuditableEntityBase`  | `Id`, `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt` | 需要审计的实体       |
| `SoftDeleteEntityBase` | `AuditableEntityBase` + `IsDeleted`                        | 需要软删除的实体     |

## 接口说明

| 接口           | 字段                                                 | 说明         |
| -------------- | ---------------------------------------------------- | ------------ |
| `IEntity`      | `Id`                                                 | 实体标识     |
| `IAuditable`   | `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt` | 审计信息     |
| `ISoftDelete`  | `IsDeleted`                                          | 软删除标记   |
| `IVersionable` | `RowVersion`                                         | 乐观并发控制 |

## 注意事项

1. **软删除全局过滤器**：继承 `ISoftDelete` 的实体查询时会自动过滤 `IsDeleted = true` 的记录
2. **审计字段**：需要在构造函数中传入 `IAuditContextProvider` 才能自动填充
3. **并发控制**：使用 `IVersionable` 接口时，EF Core 会在更新时检查 `RowVersion`，发生冲突会抛出 `DbUpdateConcurrencyException`
4. **时区**：审计字段使用 UTC 时间（`DateTime.UtcNow`）
