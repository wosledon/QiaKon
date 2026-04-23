namespace QiaKon.Cache;

/// <summary>
/// 缓存抽象接口
/// </summary>
public interface ICache
{
    /// <summary>
    /// 获取缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存值，如果不存在则返回 default</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="options">缓存选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除缓存项
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果删除成功返回 true，否则返回 false</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查缓存项是否存在
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果存在返回 true，否则返回 false</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取或创建缓存值（带工厂函数）
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">创建缓存值的工厂函数</param>
    /// <param name="options">缓存选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存值或新创建的值</returns>
    async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory();
        await SetAsync(key, value, options, cancellationToken);
        return value;
    }

    /// <summary>
    /// 批量获取缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="keys">缓存键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>键值对字典，不存在的键不会包含在结果中</returns>
    Task<Dictionary<string, T>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量设置缓存值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="items">键值对集合</param>
    /// <param name="options">缓存选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetManyAsync<T>(
        Dictionary<string, T> items,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
