namespace QiaKon.Contracts.DTOs;

/// <summary>
/// RAG检索请求
/// </summary>
public sealed record RetrieveRequestDto(
    string Query,
    int TopK = 5,
    double MinScore = 0.0,
    Guid? DepartmentId = null);

/// <summary>
/// RAG检索结果项
/// </summary>
public sealed record RetrieveResultItemDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    string Text,
    double Score,
    int StartIndex,
    int EndIndex);

/// <summary>
/// RAG检索响应
/// </summary>
public sealed record RetrieveResponseDto(
    IReadOnlyList<RetrieveResultItemDto> Results,
    int TotalCount,
    int QueryTime);

/// <summary>
/// RAG问答请求
/// </summary>
public sealed record RagChatRequestDto(
    string Query,
    Guid? ConversationId = null,
    int TopK = 5,
    Guid? ModelId = null);

/// <summary>
/// RAG问答来源
/// </summary>
public sealed record RagSourceDto(
    Guid DocumentId,
    string Title,
    string Text,
    double Score,
    string Snippet);

/// <summary>
/// RAG问答响应
/// </summary>
public sealed record RagChatResponseDto(
    string Response,
    IReadOnlyList<RagSourceDto> Sources,
    Guid ConversationId,
    int Turns);

/// <summary>
/// 文档重建索引请求
/// </summary>
public sealed record ReindexRequestDto(
    Guid? DocumentId = null);

/// <summary>
/// 文档重建索引响应
/// </summary>
public sealed record ReindexResponseDto(
    int DocumentsQueued,
    string Message);
