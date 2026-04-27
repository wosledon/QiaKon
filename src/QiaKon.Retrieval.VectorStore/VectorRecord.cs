namespace QiaKon.Retrieval.VectorStore;

/// <summary>
/// 向量记录，表示存储在向量数据库中的单条数据
/// </summary>
public sealed record VectorRecord
{
    /// <summary>
    /// 记录唯一标识
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// 向量嵌入（Embedding）
    /// </summary>
    public required ReadOnlyMemory<float> Embedding { get; init; }

    /// <summary>
    /// 原始文本内容（可选）
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// 附加元数据（键值对）
    /// </summary>
    public Dictionary<string, object?> Metadata { get; init; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
