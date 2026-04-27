using Microsoft.EntityFrameworkCore;
using QiaKon.Contracts;

namespace QiaKon.EntityFrameworkCore;

/// <summary>
/// EF Core 扩展方法
/// </summary>
public static class QiaKonDbContextExtensions
{
    /// <summary>
    /// 根据 ID 查找实体（包含已删除的）
    /// </summary>
    public static async Task<TEntity?> FindByIdAsync<TEntity>(
        this DbContext context,
        Guid id,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var query = context.Set<TEntity>().AsQueryable();

        if (!includeDeleted && typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            query = query.Where(e => !(e as ISoftDelete)!.IsDeleted);
        }

        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// 检查实体是否存在
    /// </summary>
    public static async Task<bool> ExistsAsync<TEntity>(
        this DbContext context,
        Guid id,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await context.Set<TEntity>().AnyAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// 分页查询辅助方法
    /// </summary>
    public static async Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync<TEntity>(
        this IQueryable<TEntity> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// 确保事务已开启
    /// </summary>
    public static async Task<T> ExecuteInTransactionAsync<T>(
        this DbContext context,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction != null)
        {
            return await action();
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 批量插入优化（使用 EF Core 8+ AddRange）
    /// </summary>
    public static async Task<int> BulkInsertAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        await context.Set<TEntity>().AddRangeAsync(entities, cancellationToken);
        return await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批量更新
    /// </summary>
    public static async Task<int> BulkUpdateAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        context.Set<TEntity>().UpdateRange(entities);
        return await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    public static async Task<int> BulkDeleteAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        context.Set<TEntity>().RemoveRange(entities);
        return await context.SaveChangesAsync(cancellationToken);
    }
}
