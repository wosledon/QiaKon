namespace QiaKon.Retrieval;

/// <summary>
/// 文档分块接口，表示从原始文档中拆分出的语义片段
/// </summary>
public interface IChunk
{
    /// <summary>
    /// 块唯一标识
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 所属文档ID
    /// </summary>
    Guid DocumentId { get; }

    /// <summary>
    /// 块内容（文本形式，用于Embedding和检索）
    /// </summary>
    string Text { get; }

    /// <summary>
    /// 块在文档中的起始位置（字符索引）
    /// </summary>
    int StartIndex { get; }

    /// <summary>
    /// 块在文档中的结束位置（字符索引）
    /// </summary>
    int EndIndex { get; }

    /// <summary>
    /// 块序号（在文档中的顺序）
    /// </summary>
    int Sequence { get; }

    /// <summary>
    /// 附加元数据（可包含章节标题、关键词等）
    /// </summary>
    Dictionary<string, object?> Metadata { get; }
}

/// <summary>
/// 文档块默认实现
/// </summary>
public sealed record Chunk : IChunk
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid DocumentId { get; init; }
    public required string Text { get; init; }
    public int StartIndex { get; init; }
    public int EndIndex { get; init; }
    public int Sequence { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
