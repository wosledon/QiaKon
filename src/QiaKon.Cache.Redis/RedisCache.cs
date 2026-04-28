using System.Text.Json;
using StackExchange.Redis;
using QiaKon.Cache;

namespace QiaKon.Cache.Redis;

/// <summary>
/// 基于 StackExchange.Redis 的 Redis 缓存实现
/// </summary>
public sealed class RedisCache : ICache
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _keyPrefix;

    public RedisCache(IConnectionMultiplexer connection, int database = 0, string keyPrefix = "qiakon:")
    {
        _connection = connection;
        _database = connection.GetDatabase(database);
        _keyPrefix = keyPrefix;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    private string PrefixKey(string key) => $"{_keyPrefix}{key}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = PrefixKey(key);
        var value = await _database.StringGetAsync(prefixedKey);
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>((string)value!, _jsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prefixedKey = PrefixKey(key);
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var expiry = GetExpiry(options);

        await _database.StringSetAsync(prefixedKey, json, expiry);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = PrefixKey(key);
        return await _database.KeyDeleteAsync(prefixedKey);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = PrefixKey(key);
        return await _database.KeyExistsAsync(prefixedKey);
    }

    public async Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keyArray = keys.ToArray();
        var redisKeys = keyArray.Select(k => (RedisKey)PrefixKey(k)).ToArray();
        var values = await _database.StringGetAsync(redisKeys);

        var result = new Dictionary<string, T>();
        for (int i = 0; i < keyArray.Length; i++)
        {
            if (!values[i].IsNullOrEmpty)
            {
                var value = JsonSerializer.Deserialize<T>((string)values[i]!, _jsonOptions);
                if (value is not null)
                {
                    result[keyArray[i]] = value;
                }
            }
        }

        return result;
    }

    public async Task SetManyAsync<T>(Dictionary<string, T> items, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var expiry = GetExpiry(options);

        foreach (var (key, value) in items)
        {
            var prefixedKey = PrefixKey(key);
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _database.StringSetAsync(prefixedKey, json, expiry);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // Redis 不支持直接清空单个 database，需要遍历所有 key 删除
        // 生产环境建议谨慎使用，避免误删其他数据
        var endpoints = _connection.GetEndPoints();
        foreach (var endpoint in endpoints)
        {
            var server = _connection.GetServer(endpoint);
            if (server.IsReplica)
            {
                continue;
            }

            var keys = server.Keys(database: _database.Database, pattern: $"{_keyPrefix}*").ToArray();
            if (keys.Length > 0)
            {
                _database.KeyDelete(keys);
            }
        }

        return Task.CompletedTask;
    }

    private static TimeSpan? GetExpiry(CacheEntryOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        // 优先使用绝对过期时间
        if (options.AbsoluteExpiration.HasValue)
        {
            return options.AbsoluteExpiration.Value;
        }

        // 滑动过期在 Redis 中通过设置 TTL 实现
        if (options.SlidingExpiration.HasValue)
        {
            return options.SlidingExpiration.Value;
        }

        return null;
    }
}
