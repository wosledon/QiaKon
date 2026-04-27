using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace QiaKon.EntityFrameworkCore.Npgsql;

/// <summary>
/// PostgreSQL 数据库操作扩展
/// </summary>
public static class QiaKonNpgsqlExtensions
{
    /// <summary>
    /// 检查数据库连接是否正常
    /// </summary>
    public static async Task<bool> IsDatabaseAvailableAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取数据库大小（MB）
    /// </summary>
    public static async Task<double> GetDatabaseSizeInMbAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_database_size(current_database()) / 1024.0 / 1024.0;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToDouble(result);
    }

    /// <summary>
    /// 获取表大小信息
    /// </summary>
    public static async Task<List<TableSizeInfo>> GetTableSizesAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                relname as table_name,
                pg_size_pretty(pg_total_relation_size(relid)) As total_size,
                pg_size_pretty(pg_table_size(relid)) As table_size,
                pg_size_pretty(pg_indexes_size(relid)) As index_size
            FROM pg_catalog.pg_statio_user_tables
            ORDER BY pg_total_relation_size(relid) DESC;";

        var results = new List<TableSizeInfo>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TableSizeInfo
            {
                TableName = reader.GetString(0),
                TotalSize = reader.GetString(1),
                TableSize = reader.GetString(2),
                IndexSize = reader.GetString(3)
            });
        }

        return results;
    }

    /// <summary>
    /// 执行 VACUUM ANALYZE 优化表
    /// </summary>
    public static async Task VacuumAnalyzeAsync(
        this DbContext context,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"VACUUM ANALYZE {tableName};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 获取索引使用情况
    /// </summary>
    public static async Task<List<IndexUsageInfo>> GetIndexUsageAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                schemaname,
                relname as table_name,
                indexrelname as index_name,
                idx_scan,
                idx_tup_read,
                idx_tup_fetch
            FROM pg_stat_user_indexes
            ORDER BY idx_scan DESC;";

        var results = new List<IndexUsageInfo>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new IndexUsageInfo
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                IndexName = reader.GetString(2),
                Scans = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                TupRead = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                TupFetch = reader.IsDBNull(5) ? 0 : reader.GetInt64(5)
            });
        }

        return results;
    }
}

/// <summary>
/// 表大小信息
/// </summary>
public record TableSizeInfo
{
    public string TableName { get; init; } = string.Empty;
    public string TotalSize { get; init; } = string.Empty;
    public string TableSize { get; init; } = string.Empty;
    public string IndexSize { get; init; } = string.Empty;
}

/// <summary>
/// 索引使用信息
/// </summary>
public record IndexUsageInfo
{
    public string SchemaName { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public long Scans { get; init; }
    public long TupRead { get; init; }
    public long TupFetch { get; init; }
}
