using System.Collections.Concurrent;
using StackExchange.Redis;

namespace QiaKon.Cache.Hybrid;

/// <summary>
/// 多级缓存实现（版本号 + Pub/Sub 失效通知）
/// </summary>
public sealed class HybridCache : ICache, IDisposable
{
    private readonly ICache _l1Cache;
    private readonly ICache _l2Cache;
    private readonly ISubscriber _subscriber;
    private readonly HybridCacheOptions _options;
    private readonly ConcurrentDictionary<string, long> _localVersions = new();
    private long _versionCounter;
    private bool _disposed;

    /// <summary>
    /// 创建多级缓存实例
    /// </summary>
    /// <param name="l1Cache">L1 内存缓存</param>
    /// <param name="l2Cache">L2 Redis 缓存</param>
    /// <param name="redis">Redis 连接（用于 Pub/Sub）</param>
    /// <param name="options">缓存配置选项</param>
    public HybridCache(
        ICache l1Cache,
        ICache l2Cache,
        IConnectionMultiplexer redis,
        HybridCacheOptions? options = null)
    {
        _l1Cache = l1Cache ?? throw new ArgumentNullException(nameof(l1Cache));
        _l2Cache = l2Cache ?? throw new ArgumentNullException(nameof(l2Cache));
        _options = options ?? HybridCacheOptions.Default;
        _subscriber = redis?.GetSubscriber() ?? throw new ArgumentNullException(nameof(redis));

        // 订阅全局失效通道
        _subscriber.Subscribe(RedisChannel.Literal(_options.InvalidationChannel), OnInvalidationMessage);
    }

    /// <summary>
    /// 获取缓存值
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        // 1. 尝试从 L1 获取
        if (await TryGetFromL1Async<T>(key, cancellationToken))
        {
            var l1Value = await _l1Cache.GetAsync<T>(key, cancellationToken);
            return l1Value;
        }

        // 2. 尝试从 L2 获取
        var l2Value = await _l2Cache.GetAsync<T>(key, cancellationToken);
        if (l2Value is not null)
        {
            // 更新本地版本号
            var version = await GetVersionAsync(key, cancellationToken);
            _localVersions[key] = version;

            // 回填 L1
            if (_options.EnableL1Backfill)
            {
                await _l1Cache.SetAsync(key, l2Value, GetL1Options(), cancellationToken);
            }

            return l2Value;
        }

        // 3. 都未命中
        return default;
    }

    /// <summary>
    /// 设置缓存值（写入 L1 + L2 + 版本号 + Pub/Sub 通知）
    /// </summary>
    public async Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        // 递增版本号
        var version = Interlocked.Increment(ref _versionCounter);

        // 并行写入 L2 和版本号
        var l2Task = _l2Cache.SetAsync(key, value, GetL2Options(options), cancellationToken);
        var versionTask = SetVersionAsync(key, version, cancellationToken);

        await Task.WhenAll(l2Task, versionTask);

        // 发布失效通知（通知其他节点）
        await _subscriber.PublishAsync(RedisChannel.Literal(_options.InvalidationChannel), key);

        // 更新本地版本并写入 L1
        _localVersions[key] = version;
        await _l1Cache.SetAsync(key, value, GetL1Options(options), cancellationToken);
    }

    /// <summary>
    /// 删除缓存（通知其他节点 + 删除本地 + 删除 L2）
    /// </summary>
    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        // 通知其他节点失效
        await _subscriber.PublishAsync(RedisChannel.Literal(_options.InvalidationChannel), key);

        // 清理本地版本
        _localVersions.TryRemove(key, out _);

        // 并行删除 L1 和 L2
        var l1Task = _l1Cache.RemoveAsync(key, cancellationToken);
        var l2Task = _l2Cache.RemoveAsync(key, cancellationToken);

        await Task.WhenAll(l1Task, l2Task);

        return l1Task.Result || l2Task.Result;
    }

    /// <summary>
    /// 检查缓存是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (await _l1Cache.ExistsAsync(key, cancellationToken))
        {
            return true;
        }

        return await _l2Cache.ExistsAsync(key, cancellationToken);
    }

    /// <summary>
    /// 批量获取缓存值
    /// </summary>
    public async Task<Dictionary<string, T>> GetManyAsync<T>(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var result = new Dictionary<string, T>();
        var missedKeys = new List<string>();

        // 1. 批量从 L1 获取
        var l1Results = await _l1Cache.GetManyAsync<T>(keyList, cancellationToken);
        foreach (var (key, value) in l1Results)
        {
            result[key] = value;
        }

        // 2. 找出未命中的键
        missedKeys = keyList.Where(k => !result.ContainsKey(k)).ToList();
        if (missedKeys.Count == 0)
        {
            return result;
        }

        // 3. 批量从 L2 获取
        var l2Results = await _l2Cache.GetManyAsync<T>(missedKeys, cancellationToken);
        var l1Backfill = new Dictionary<string, T>();

        foreach (var (key, value) in l2Results)
        {
            result[key] = value;
            l1Backfill[key] = value;

            // 更新本地版本
            var version = await GetVersionAsync(key, cancellationToken);
            _localVersions[key] = version;
        }

        // 4. 回填 L1
        if (l1Backfill.Count > 0 && _options.EnableL1Backfill)
        {
            await _l1Cache.SetManyAsync(l1Backfill, GetL1Options(), cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// 批量设置缓存值
    /// </summary>
    public async Task SetManyAsync<T>(
        Dictionary<string, T> items,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _versionCounter);

        // 并行写入 L2 和更新所有版本号
        var tasks = new List<Task>(items.Count + 1);
        tasks.Add(_l2Cache.SetManyAsync(items, GetL2Options(options), cancellationToken));

        foreach (var key in items.Keys)
        {
            tasks.Add(SetVersionAsync(key, version, cancellationToken));
        }

        await Task.WhenAll(tasks);

        // 发布所有 key 的失效通知
        foreach (var key in items.Keys)
        {
            await _subscriber.PublishAsync(RedisChannel.Literal(_options.InvalidationChannel), key);
        }

        // 更新本地版本并写入 L1
        foreach (var key in items.Keys)
        {
            _localVersions[key] = version;
        }

        await _l1Cache.SetManyAsync(items, GetL1Options(options), cancellationToken);
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // 发布清空通知
        await _subscriber.PublishAsync(RedisChannel.Literal(_options.InvalidationChannel), "*:clear");

        // 清空本地版本
        _localVersions.Clear();

        // 并行清空 L1 和 L2
        await Task.WhenAll(
            _l1Cache.ClearAsync(cancellationToken),
            _l2Cache.ClearAsync(cancellationToken)
        );
    }

    /// <summary>
    /// 尝试从 L1 获取（带版本校验）
    /// </summary>
    private async Task<bool> TryGetFromL1Async<T>(string key, CancellationToken ct)
    {
        if (!await _l1Cache.ExistsAsync(key, ct))
        {
            return false;
        }

        // 如果启用版本检查，校验 L1 数据是否过期
        if (_options.EnableVersionCheck)
        {
            var l2Version = await GetVersionAsync(key, ct);
            var localVersion = _localVersions.GetValueOrDefault(key, 0);

            // L2 版本更高，说明 L1 已过期
            if (l2Version > localVersion && l2Version > 0)
            {
                // 删除过期的 L1 数据
                await _l1Cache.RemoveAsync(key, ct);
                _localVersions.TryRemove(key, out _);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 获取 L2 存储的版本号
    /// </summary>
    private async Task<long> GetVersionAsync(string key, CancellationToken ct)
    {
        var versionKey = $"{_options.VersionPrefix}{key}";
        var versionStr = await _l2Cache.GetAsync<string>(versionKey, ct);
        return long.TryParse(versionStr, out var version) ? version : 0;
    }

    /// <summary>
    /// 设置 L2 版本号
    /// </summary>
    private async Task SetVersionAsync(string key, long version, CancellationToken ct)
    {
        var versionKey = $"{_options.VersionPrefix}{key}";
        await _l2Cache.SetAsync(versionKey, version.ToString(), null, ct);
    }

    /// <summary>
    /// 获取 L1 缓存选项
    /// </summary>
    private CacheEntryOptions? GetL1Options(CacheEntryOptions? options = null)
    {
        return _options.L1Options?.Clone() ?? options;
    }

    /// <summary>
    /// 获取 L2 缓存选项
    /// </summary>
    private CacheEntryOptions? GetL2Options(CacheEntryOptions? options = null)
    {
        return _options.L2Options?.Clone() ?? options;
    }

    /// <summary>
    /// 处理 Redis Pub/Sub 失效通知
    /// </summary>
    private void OnInvalidationMessage(RedisChannel channel, RedisValue key)
    {
        var keyStr = key.ToString();

        // 清空通知
        if (keyStr.EndsWith(":clear"))
        {
            _localVersions.Clear();
            _l1Cache.ClearAsync().ConfigureAwait(false);
            return;
        }

        // 单个 key 失效
        _localVersions.TryRemove(keyStr, out _);
        _l1Cache.RemoveAsync(keyStr).ConfigureAwait(false);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _subscriber.Unsubscribe(RedisChannel.Literal(_options.InvalidationChannel));
        _disposed = true;
    }
}
