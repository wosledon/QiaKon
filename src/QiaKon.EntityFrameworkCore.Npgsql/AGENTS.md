# QiaKon.EntityFrameworkCore.Npgsql - AGENTS.md

> **模块**: EF Core PostgreSQL 集成  
> **职责**: EF Core DbContext 配置、实体映射、迁移管理  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Connector.Npgsql`  
> **被依赖**: `QiaKon.Api`, 业务模块

---

## 一、模块职责

本模块提供 EF Core 与 PostgreSQL 的集成配置，包括 DbContext 配置、实体映射和迁移管理。

**核心职责**:
- `QiaKonNpgsqlDbContext` 实现
- 实体配置
- 数据库迁移
- 仓储模式封装

---

## 二、核心实现

### 2.1 QiaKonNpgsqlDbContext

```csharp
public class QiaKonNpgsqlDbContext : DbContext
{
    public DbSet<Document> Documents { get; set; }
    public DbSet<Entity> GraphEntities { get; set; }
    public DbSet<Relation> GraphRelations { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Department> Departments { get; set; }
    
    public QiaKonNpgsqlDbContext(DbContextOptions<QiaKonNpgsqlDbContext> options)
        : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // 应用所有实体配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QiaKonNpgsqlDbContext).Assembly);
    }
}
```

### 2.2 扩展方法

```csharp
public static class QiaKonNpgsqlExtensions
{
    public static IServiceCollection AddQiaKonNpgsql(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<QiaKonNpgsqlDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(QiaKonNpgsqlDbContext).Assembly.FullName);
                npgsql.UseVector(); // pgvector 扩展
            }));
        
        return services;
    }
}
```

---

## 三、实体配置

### 3.1 文档实体配置

```csharp
public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        
        builder.HasKey(d => d.Id);
        
        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(d => d.Content)
            .HasColumnType("text");
        
        builder.HasIndex(d => d.DepartmentId);
        builder.HasIndex(d => d.CreatedAt);
        
        // 软删除全局查询过滤器
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
```

---

## 四、数据库迁移

### 4.1 创建迁移

```bash
dotnet ef migrations add InitialCreate --project src/QiaKon.EntityFrameworkCore.Npgsql
```

### 4.2 应用迁移

```bash
dotnet ef database update --project src/QiaKon.EntityFrameworkCore.Npgsql
```

---

## 五、开发规范

### 5.1 仓储模式

```csharp
public interface IRepository<T> where T : class, IEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
}
```

### 5.2 查询最佳实践

- 使用 `AsNoTracking()` 提升只读查询性能
- 使用投影查询减少数据传输
- 避免 N+1 查询，使用 `Include()`

---

## 六、测试要求

- DbContext 配置
- 实体映射
- 迁移应用
- 查询性能

---

**最后更新**: 2026-04-28  
**维护者**: 数据库专家 Agent
