using QiaKon.Retrieval.VectorStore;

namespace QiaKon.Retrieval;

/// <summary>
/// RAG（检索增强生成）管道接口
/// 定义从文档处理、分块、嵌入到向量存储的完整流程
/// </summary>
public interface IRagPipeline
{
    /// <summary>
    /// 将文档索引到向量存储中
    /// </summary>
    /// <param name="document">原始文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>索引后的文档记录信息</returns>
    Task<RagDocumentRecord> IndexAsync(
        IDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将已处理的文档直接索引（跳过文档处理阶段）
    /// </summary>
    /// <param name="document">已处理的文档（Content应为可直接分块的文本）</param>
    /// <param name="skipProcessing">是否跳过文档处理</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<RagDocumentRecord> IndexAsync(
        IDocument document,
        bool skipProcessing,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据查询检索相关文档块
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="options">检索选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索结果列表</returns>
    Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        string query,
        RetrievalOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定文档及其所有块
    /// </summary>
    Task<bool> DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// RAG文档索引记录
/// </summary>
public sealed record RagDocumentRecord
{
    public required Guid DocumentId { get; init; }
    public required string Title { get; init; }
    public required int ChunkCount { get; init; }
    public required DateTimeOffset IndexedAt { get; init; }
    public required IReadOnlyList<Guid> ChunkIds { get; init; }
}

/// <summary>
/// RAG检索结果
/// </summary>
public sealed record RagSearchResult
{
    /// <summary>
    /// 匹配的文档块
    /// </summary>
    public required IChunk Chunk { get; init; }

    /// <summary>
    /// 相似度得分（0~1，越高越相似）
    /// </summary>
    public required float Score { get; init; }

    /// <summary>
    /// 关联的原始文档
    /// </summary>
    public IDocument? Document { get; init; }
}
