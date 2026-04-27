namespace QiaKon.Retrieval.Embedding;

/// <summary>
/// 文本嵌入服务接口
/// 负责将文本转换为向量嵌入（Embedding）
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// 嵌入模型维度
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// 嵌入模型名称
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// 将单条文本转换为向量嵌入
    /// </summary>
    Task<ReadOnlyMemory<float>> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量将文本转换为向量嵌入
    /// </summary>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);
}
