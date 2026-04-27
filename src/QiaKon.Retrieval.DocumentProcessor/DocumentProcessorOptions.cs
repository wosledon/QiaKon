namespace QiaKon.Retrieval.DocumentProcessor;

/// <summary>
/// 文档处理器配置选项
/// </summary>
public sealed class DocumentProcessorOptions
{
    /// <summary>
    /// 最大处理文件大小（字节，默认 100MB）
    /// </summary>
    public long MaxFileSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// 是否保留原始文档中的图片引用
    /// </summary>
    public bool KeepImageReferences { get; set; } = true;

    /// <summary>
    /// 是否提取文档中的表格数据
    /// </summary>
    public bool ExtractTables { get; set; } = true;
}
