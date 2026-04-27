namespace QiaKon.Retrieval.DocumentProcessor;

/// <summary>
/// 文档处理器接口：将各种格式文档转换为标准化文本（Markdown/纯文本）
/// </summary>
public interface IDocumentProcessor
{
    /// <summary>
    /// 支持的MIME类型列表
    /// </summary>
    IReadOnlyList<string> SupportedMimeTypes { get; }

    /// <summary>
    /// 处理文档文件，将其转换为标准化的文本内容
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理后的文档</returns>
    Task<ProcessedDocument> ProcessFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理二进制流，将其转换为标准化的文本内容
    /// </summary>
    /// <param name="stream">文档二进制流</param>
    /// <param name="mimeType">MIME类型</param>
    /// <param name="fileName">原始文件名（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理后的文档</returns>
    Task<ProcessedDocument> ProcessStreamAsync(
        Stream stream,
        string mimeType,
        string? fileName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否支持指定的MIME类型
    /// </summary>
    bool CanProcess(string mimeType);
}

/// <summary>
/// 处理后的文档结果
/// </summary>
public sealed record ProcessedDocument
{
    /// <summary>
    /// 文档标题
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 处理后的Markdown/纯文本内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 原始MIME类型
    /// </summary>
    public required string OriginalMimeType { get; init; }

    /// <summary>
    /// 处理后的文本格式（通常是text/markdown或text/plain）
    /// </summary>
    public required string ProcessedMimeType { get; init; }

    /// <summary>
    /// 处理过程中提取的元数据
    /// </summary>
    public Dictionary<string, object?> Metadata { get; init; } = new();
}
