using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QiaKon.Contracts;

namespace QiaKon.EntityFrameworkCore;

/// <summary>
/// EF Core DbContext 基类，提供审计字段自动填充、软删除查询过滤、乐观并发等标准化功能
/// </summary>
public abstract class QiaKonDbContext : DbContext
{
    private readonly IAuditContextProvider? _auditContextProvider;

    protected QiaKonDbContext(DbContextOptions options) : base(options)
    {
    }

    protected QiaKonDbContext(DbContextOptions options, IAuditContextProvider? auditContextProvider = null) : base(options)
    {
        _auditContextProvider = auditContextProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置所有软删除实体的全局查询过滤器
        ApplySoftDeleteFilter(modelBuilder);

        // 配置审计字段
        ConfigureAuditableEntities(modelBuilder);

        // 配置版本字段
        ConfigureVersionableEntities(modelBuilder);
    }

    /// <summary>
    /// 应用软删除全局查询过滤器
    /// </summary>
    private void ApplySoftDeleteFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                var filter = Expression.Lambda(Expression.Equal(property, Expression.Constant(false)), parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    /// <summary>
    /// 配置可审计实体的默认值
    /// </summary>
    private void ConfigureAuditableEntities(ModelBuilder modelBuilder)
    {
        // 审计字段由数据库或应用层填充，不需要默认值
        // CreatedAt 会在 ApplyAuditInfo 中自动设置
    }

    /// <summary>
    /// 配置版本控制实体的并发令牌
    /// </summary>
    private void ConfigureVersionableEntities(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IVersionable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IVersionable.RowVersion))
                    .IsRowVersion();
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    /// <summary>
    /// 在保存前自动填充审计字段
    /// </summary>
    private void ApplyAuditInfo()
    {
        var userId = _auditContextProvider?.GetCurrentUserId() ?? Guid.Empty;
        var now = DateTime.UtcNow;

        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedBy = userId;
                    auditable.CreatedAt = now;
                }

                auditable.ModifiedBy = userId;
                auditable.ModifiedAt = now;
            }
        }
    }

    /// <summary>
    /// 软删除实体的扩展方法 - 将 IsDeleted 设置为 true 而不是真正删除
    /// </summary>
    public void SoftDelete<TEntity>(TEntity entity) where TEntity : class, ISoftDelete
    {
        entity.IsDeleted = true;
        Entry(entity).State = EntityState.Modified;
    }

    /// <summary>
    /// 包含已删除实体的查询（禁用软删除过滤器）
    /// </summary>
    public IQueryable<TEntity> WithDeleted<TEntity>() where TEntity : class
    {
        return Set<TEntity>().IgnoreQueryFilters();
    }

    /// <summary>
    /// 仅查询已删除的实体
    /// </summary>
    public IQueryable<TEntity> OnlyDeleted<TEntity>() where TEntity : class, ISoftDelete
    {
        return Set<TEntity>().IgnoreQueryFilters().Where(e => e.IsDeleted);
    }

    /// <summary>
    /// 批量软删除
    /// </summary>
    public async Task<int> SoftDeleteRangeAsync<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        where TEntity : class, ISoftDelete
    {
        foreach (var entity in entities)
        {
            entity.IsDeleted = true;
            Entry(entity).State = EntityState.Modified;
        }

        return await SaveChangesAsync(cancellationToken);
    }
}
