namespace QiaKon.Llm;

/// <summary>
/// LLM Provider基类
/// </summary>
public abstract class BaseLLMProvider : ILLMProvider
{
    public abstract string ProviderName { get; }
    public abstract ProviderType ProviderType { get; }

    public abstract Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<StreamEvent> CompleteStreamingAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    public abstract Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected abstract void Dispose(bool disposing);
}
