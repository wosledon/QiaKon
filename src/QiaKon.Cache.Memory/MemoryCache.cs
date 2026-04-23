using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using QiaKon.Cache;

namespace QiaKon.Cache.Memory;

/// <summary>
/// 基于 Microsoft.Extensions.Caching.Memory 的内存缓存实现
/// </summary>
public sealed class MemoryCache : ICache
{
    private readonly IMemoryCache _cache;

    public MemoryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            _cache.TryGetValue<T>(key, out var value) ? value : default);
    }

    public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entryOptions = CreateMemoryCacheEntryOptions(options);
        _cache.Set(key, value, entryOptions);

        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existed = _cache.TryGetValue(key, out _);
        if (existed)
        {
            _cache.Remove(key);
        }

        return Task.FromResult(existed);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    public Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new Dictionary<string, T>();
        foreach (var key in keys)
        {
            if (_cache.TryGetValue<T>(key, out var value))
            {
                result[key] = value!;
            }
        }

        return Task.FromResult(result);
    }

    public Task SetManyAsync<T>(Dictionary<string, T> items, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entryOptions = CreateMemoryCacheEntryOptions(options);
        foreach (var (key, value) in items)
        {
            _cache.Set(key, value, entryOptions);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // IMemoryCache 不支持全局清空，这里标记为已清理
        // 如果需要真正的清空，建议使用自定义的 MemoryCache 实现
        if (_cache is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return Task.CompletedTask;
    }

    private static Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions CreateMemoryCacheEntryOptions(CacheEntryOptions? options)
    {
        var entryOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions();

        if (options is null)
        {
            return entryOptions;
        }

        if (options.AbsoluteExpiration.HasValue)
        {
            entryOptions.SetAbsoluteExpiration(options.AbsoluteExpiration.Value);
        }

        if (options.SlidingExpiration.HasValue)
        {
            entryOptions.SetSlidingExpiration(options.SlidingExpiration.Value);
        }

        // 简化优先级处理，MemoryCacheEntryOptions 使用 int 值
        // Low=1, Normal=2, High=3, NeverRemove=4
        entryOptions.Priority = (Microsoft.Extensions.Caching.Memory.CacheItemPriority)(int)options.Priority;

        return entryOptions;
    }
}
