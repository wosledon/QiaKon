using Microsoft.EntityFrameworkCore;
using QiaKon.EntityFrameworkCore;

namespace QiaKon.EntityFrameworkCore.Npgsql;

/// <summary>
/// PostgreSQL 特定的 DbContext 基类，针对 Npgsql 进行优化
/// </summary>
public abstract class QiaKonNpgsqlDbContext : QiaKonDbContext
{
    protected QiaKonNpgsqlDbContext(DbContextOptions options) : base(options)
    {
    }

    protected QiaKonNpgsqlDbContext(DbContextOptions options, IAuditContextProvider? auditContextProvider = null) : base(options, auditContextProvider)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Npgsql 特定优化配置会在外部通过 AddQiaKonNpgsqlDbContext 配置
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置 PostgreSQL 特定的类型映射
        ConfigureNpgsqlMappings(modelBuilder);
    }

    /// <summary>
    /// 配置 PostgreSQL 特定的类型映射
    /// </summary>
    private void ConfigureNpgsqlMappings(ModelBuilder modelBuilder)
    {
        // UUID 主键优化
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var idProperty = entityType.FindProperty("Id");
            if (idProperty?.ClrType == typeof(Guid))
            {
                idProperty.SetColumnType("uuid");
            }
        }
    }
}
