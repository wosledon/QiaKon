namespace QiaKon.Retrieval;

/// <summary>
/// 文档分块策略接口
/// </summary>
public interface IChunkingStrategy
{
    /// <summary>
    /// 策略名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 将文档内容拆分为多个语义块
    /// </summary>
    /// <param name="documentId">文档ID</param>
    /// <param name="content">文档文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分块列表</returns>
    Task<IReadOnlyList<IChunk>> ChunkAsync(
        Guid documentId,
        string content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 分块策略配置基类
/// </summary>
public abstract class ChunkingOptionsBase
{
    /// <summary>
    /// 每个块的最大字符数（默认 2000）
    /// </summary>
    public int MaxChunkSize { get; set; } = 2000;

    /// <summary>
    /// 块之间的重叠字符数（默认 200）
    /// </summary>
    public int OverlapSize { get; set; } = 200;

    /// <summary>
    /// 是否保留章节标题作为元数据
    /// </summary>
    public bool PreserveHeaders { get; set; } = true;

    /// <summary>
    /// 最小块大小，小于此值的块将被合并（默认 100）
    /// </summary>
    public int MinChunkSize { get; set; } = 100;
}
