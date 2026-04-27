namespace QiaKon.Retrieval;

/// <summary>
/// 文档抽象，表示RAG系统中的原始文档
/// </summary>
public interface IDocument
{
    /// <summary>
    /// 文档唯一标识
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 文档标题
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// 文档原始内容（可能为Markdown、纯文本或二进制数据的描述）
    /// </summary>
    string Content { get; }

    /// <summary>
    /// 文档来源（文件路径、URL等）
    /// </summary>
    string? Source { get; }

    /// <summary>
    /// 文档MIME类型
    /// </summary>
    string? MimeType { get; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    Dictionary<string, object?> Metadata { get; }

    /// <summary>
    /// 文档创建时间
    /// </summary>
    DateTimeOffset CreatedAt { get; }
}

/// <summary>
/// 文档默认实现
/// </summary>
public sealed record Document : IDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Title { get; init; }
    public required string Content { get; init; }
    public string? Source { get; init; }
    public string? MimeType { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
