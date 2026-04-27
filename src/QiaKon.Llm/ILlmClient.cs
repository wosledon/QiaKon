namespace QiaKon.Llm;

/// <summary>
/// LLM客户端接口
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// 供应商类型
    /// </summary>
    LlmProviderType Provider { get; }

    /// <summary>
    /// 模型名称
    /// </summary>
    string Model { get; }

    /// <summary>
    /// 发送Chat补全请求
    /// </summary>
    Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送流式Chat补全请求
    /// </summary>
    IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放资源
    /// </summary>
    ValueTask DisposeAsync();
}

/// <summary>
/// LLM客户端工厂接口
/// </summary>
public interface ILlmClientFactory
{
    /// <summary>
    /// 创建客户端
    /// </summary>
    ILlmClient CreateClient(LlmOptions options);

    /// <summary>
    /// 创建客户端（带生命周期管理）
    /// </summary>
    ManagedLlmClient CreateManagedClient(LlmOptions options);
}

/// <summary>
/// 带生命周期管理的客户端
/// </summary>
public sealed class ManagedLlmClient : IAsyncDisposable
{
    private readonly ILlmClient _client;
    private readonly SemaphoreSlim _semaphore;

    public ManagedLlmClient(ILlmClient client, int maxConcurrency)
    {
        _client = client;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public LlmProviderType Provider => _client.Provider;
    public string Model => _client.Model;

    public async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await _client.CompleteAsync(request, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async IAsyncEnumerable<ChatCompletionChunk> CompleteStreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await foreach (var chunk in _client.CompleteStreamAsync(request, cancellationToken))
            {
                yield return chunk;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        await _client.DisposeAsync();
    }
}
