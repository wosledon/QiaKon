using Microsoft.Extensions.Logging;
using QiaKon.Llm;

namespace QiaKon.Retrieval.Chunnking.MoE;

/// <summary>
/// MoE 分块策略工厂接口
/// </summary>
public interface IMoEChunkingStrategyFactory
{
    /// <summary>
    /// 创建分块策略（使用调用方提供的 ILlmClient）
    /// </summary>
    IMoEChunkingStrategy Create(ILlmClient llmClient, MoEChunkingOptions options);

    /// <summary>
    /// 创建分块策略（使用 ILLMProvider，工厂管理生命周期）
    /// </summary>
    IMoEChunkingStrategy Create(ILLMProvider provider, MoEChunkingOptions options);
}

/// <summary>
/// MoE 分块策略工厂
/// 管理 LLM 客户端生命周期，支持单例复用
/// </summary>
public sealed class MoEChunkingStrategyFactory : IMoEChunkingStrategyFactory
{
    private readonly ILoggerFactory? _loggerFactory;
    private readonly Dictionary<string, (ILLMProvider Provider, LlmClientAdapter Adapter)> _managedProviders = new();
    private readonly object _lock = new();

    public MoEChunkingStrategyFactory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IMoEChunkingStrategy Create(ILlmClient llmClient, MoEChunkingOptions options)
    {
        return new MoEChunkingStrategy(llmClient, options);
    }

    /// <inheritdoc />
    public IMoEChunkingStrategy Create(ILLMProvider provider, MoEChunkingOptions options)
    {
        var key = GetProviderKey(provider);

        lock (_lock)
        {
            if (_managedProviders.TryGetValue(key, out var existing))
            {
                return new MoEChunkingStrategy(existing.Adapter, options);
            }

            var adapter = new LlmClientAdapter(provider);
            _managedProviders[key] = (provider, adapter);
            return new MoEChunkingStrategy(adapter, options);
        }
    }

    private static string GetProviderKey(ILLMProvider provider)
    {
        return $"{provider.Provider}:{provider.Model}";
    }

    /// <summary>
    /// 释放所有托管的 Provider
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        List<Task> disposeTasks;
        lock (_lock)
        {
            disposeTasks = _managedProviders.Values
                .Select(p => p.Provider.DisposeAsync().AsTask())
                .ToList();
            _managedProviders.Clear();
        }

        await Task.WhenAll(disposeTasks);
    }
}

/// <summary>
/// MoE 分块策略接口（与 IChunkingStrategy 兼容）
/// </summary>
public interface IMoEChunkingStrategy : IChunkingStrategy
{
}

/// <summary>
/// 托管的分块策略，自动管理 ILlmClient 生命周期
/// </summary>
internal sealed class ManagedMoEChunkingStrategy : IMoEChunkingStrategy, IAsyncDisposable
{
    private readonly MoEChunkingStrategy _strategy;
    private readonly LlmClientAdapter _adapter;
    private readonly ILLMProvider? _provider;
    private readonly bool _ownsAdapter;

    public string Name => _strategy.Name;

    public ManagedMoEChunkingStrategy(ILLMProvider provider, MoEChunkingOptions options, ILogger<MoEChunkingStrategy>? logger)
    {
        _provider = provider;
        _adapter = new LlmClientAdapter(provider);
        _ownsAdapter = true;
        _strategy = new MoEChunkingStrategy(_adapter, options, logger);
    }

    public ManagedMoEChunkingStrategy(LlmClientAdapter adapter, MoEChunkingOptions options, ILogger<MoEChunkingStrategy>? logger)
    {
        _adapter = adapter;
        _ownsAdapter = false;
        _strategy = new MoEChunkingStrategy(_adapter, options, logger);
    }

    public Task<IReadOnlyList<IChunk>> ChunkAsync(Guid documentId, string content, CancellationToken cancellationToken = default)
    {
        return _strategy.ChunkAsync(documentId, content, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsAdapter)
        {
            await _adapter.DisposeAsync();
        }
        if (_provider != null)
        {
            await _provider.DisposeAsync();
        }
    }
}

/// <summary>
/// ILLMProvider 到 ILlmClient 的适配器
/// </summary>
internal sealed class LlmClientAdapter : ILlmClient
{
    private readonly ILLMProvider _provider;

    public LlmClientAdapter(ILLMProvider provider)
    {
        _provider = provider;
    }

    public LlmProviderType Provider => _provider.Provider;
    public string Model => _provider.Model;

    public Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        return _provider.CompleteAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in _provider.CompleteStreamAsync(request, cancellationToken))
        {
            yield return chunk;
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
