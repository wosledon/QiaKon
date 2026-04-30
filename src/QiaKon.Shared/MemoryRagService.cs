using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

/// <summary>
/// 内存态RAG服务实现（基于模板拼装，无需外部LLM API）
/// </summary>
public sealed class MemoryRagService : IRagService
{
    private readonly Dictionary<Guid, ChatSessionRecord> _sessions = new();
    private readonly List<RetrievalChunk> _chunks = new();
    private readonly ILogger<MemoryRagService>? _logger;

    public MemoryRagService(ILogger<MemoryRagService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        _chunks.AddRange(
        [
            new RetrievalChunk(Guid.Parse("c1111111-1111-1111-1111-111111111111"), Guid.Parse("d1111111-1111-1111-1111-111111111111"), "QiaKon平台架构设计文档", "QiaKon是一个企业级KAG平台，将知识图谱的结构化推理能力与RAG的灵活检索能力深度融合，为企业提供准确、可信、可溯源的智能问答能力。", 0, 150),
            new RetrievalChunk(Guid.Parse("c1111112-1111-1111-1111-111111111112"), Guid.Parse("d1111111-1111-1111-1111-111111111111"), "QiaKon平台架构设计文档", "平台采用模块化架构，包括API层、服务层、数据层等多个组件。核心技术栈包括：.NET 9、ASP.NET Core、EF Core、PostgreSQL、Redis等。", 150, 280),
            new RetrievalChunk(Guid.Parse("c2222221-2222-2222-2222-222222222221"), Guid.Parse("d2222222-2222-2222-2222-222222222222"), "RAG检索管道技术方案", "RAG（检索增强生成）管道包含文档解析、分块、嵌入生成与向量存储等环节，支持PDF、Word、Markdown等格式。", 0, 200),
            new RetrievalChunk(Guid.Parse("c2222222-2222-2222-2222-222222222222"), Guid.Parse("d2222222-2222-2222-2222-222222222222"), "RAG检索管道技术方案", "向量存储使用pgvector，混合检索融合向量检索和关键词检索，并可通过Rerank提升精度。", 200, 320),
            new RetrievalChunk(Guid.Parse("c3333331-3333-3333-3333-333333333331"), Guid.Parse("d3333333-3333-3333-3333-333333333333"), "知识图谱引擎设计文档", "知识图谱引擎支持实体管理、关系管理、路径查询、多跳推理等能力，可基于内存或Npgsql后端运行。", 0, 200),
            new RetrievalChunk(Guid.Parse("c6666661-6666-6666-6666-666666666661"), Guid.Parse("d6666666-6666-6666-6666-666666666666"), "研发部项目管理制度", "研发部要求所有代码经过review后合并，单元测试覆盖率达到80%以上，重要功能需有技术文档。", 0, 160),
            new RetrievalChunk(Guid.Parse("c5555551-5555-5555-5555-555555555551"), Guid.Parse("d5555555-5555-5555-5555-555555555555"), "员工手册", "员工手册包含公司制度、福利政策、考勤规定等内容，所有员工入职后需完成岗前培训。", 0, 120),
        ]);
    }

    public RetrieveResponseDto Retrieve(RetrieveRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        var normalizedQuery = request.Query.Trim().ToLowerInvariant();
        var keywords = ExtractKeywords(normalizedQuery);

        var results = _chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = CalculateScore(normalizedQuery, keywords, chunk.Text.ToLowerInvariant()),
            })
            .Where(x => x.Score >= request.MinScore)
            .OrderByDescending(x => x.Score)
            .Take(request.TopK)
            .Select(x => new RetrieveResultItemDto(
                x.Chunk.Id,
                x.Chunk.DocumentId,
                x.Chunk.DocumentTitle,
                x.Chunk.Text,
                Math.Round(x.Score, 4),
                x.Chunk.StartIndex,
                x.Chunk.EndIndex))
            .ToList();

        stopwatch.Stop();
        return new RetrieveResponseDto(results, results.Count, (int)stopwatch.ElapsedMilliseconds);
    }

    public RagChatResponseDto Chat(RagChatRequestDto request)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid();
        if (!_sessions.TryGetValue(conversationId, out var session))
        {
            session = new ChatSessionRecord(conversationId, TruncateText(request.Query, 24), [], DateTime.UtcNow);
            _sessions[conversationId] = session;
        }

        session.Messages.Add(new ChatMessageRecord("user", request.Query));
        var retrieved = Retrieve(new RetrieveRequestDto(request.Query, request.TopK));

        if (retrieved.Results.Count == 0)
        {
            return new RagChatResponseDto("抱歉，我目前没有找到与您问题相关的信息。", Array.Empty<RagSourceDto>(), conversationId, session.Messages.Count / 2);
        }

        var response = GenerateAnswer(request.Query, retrieved.Results);
        session.Messages.Add(new ChatMessageRecord("assistant", response));
        session.UpdatedAt = DateTime.UtcNow;

        var sources = retrieved.Results
            .Select(result => new RagSourceDto(
                result.DocumentId,
                result.DocumentTitle,
                result.Text,
                result.Score,
                TruncateText(result.Text, 90)))
            .ToList();

        return new RagChatResponseDto(response, sources, conversationId, session.Messages.Count / 2);
    }

    public async IAsyncEnumerable<RagChatStreamEventDto> StreamChat(
        RagChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = Chat(request);
        foreach (var segment in SplitText(response.Response, 24))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new RagChatStreamEventDto("chunk", Delta: segment);
            await Task.Yield();
        }

        yield return new RagChatStreamEventDto(
            "done",
            Response: response.Response,
            Sources: response.Sources,
            ConversationId: response.ConversationId,
            Turns: response.Turns);
    }

    public IReadOnlyList<ConversationHistoryDto> GetConversationHistory(int offset, int limit, Guid? userId = null)
    {
        return _sessions.Values
            .OrderByDescending(s => s.UpdatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(s => new ConversationHistoryDto(s.Id, s.Title, s.Messages.Count, s.CreatedAt, s.UpdatedAt))
            .ToList();
    }

    public ConversationDetailDto? GetConversationDetail(Guid conversationId)
    {
        if (!_sessions.TryGetValue(conversationId, out var session))
            return null;

        var messages = session.Messages.Select(m => new ChatMessageDto(
            m.Id,
            m.Role,
            m.Content,
            m.CreatedAt,
            null))
            .ToList();

        return new ConversationDetailDto(session.Id, session.Title, messages, session.CreatedAt, session.UpdatedAt);
    }

    public bool DeleteConversation(Guid conversationId)
    {
        return _sessions.Remove(conversationId);
    }

    public ConversationDetailDto? UpdateConversationTitle(Guid conversationId, string title)
    {
        if (!_sessions.TryGetValue(conversationId, out var session))
        {
            return null;
        }

        session.Title = string.IsNullOrWhiteSpace(title) ? session.Title : title.Trim();
        session.UpdatedAt = DateTime.UtcNow;
        return GetConversationDetail(conversationId);
    }

    private static string GenerateAnswer(string query, IReadOnlyList<RetrieveResultItemDto> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Based on your question \"{query}\", I found the following key information:");
        sb.AppendLine();

        foreach (var result in results.Take(3))
        {
            sb.AppendLine($"- {result.DocumentTitle}: {TruncateText(result.Text, 110)}");
        }

        sb.AppendLine();
        sb.Append("If needed, I can continue helping you break down implementation steps, analyze module relationships, or summarize into shorter conclusions.");
        return sb.ToString();
    }

    private static double CalculateScore(string query, IReadOnlyCollection<string> keywords, string text)
    {
        var score = 0.0;
        if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.45;
        }

        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.15;
            }
        }

        return Math.Min(score, 1.0);
    }

    private static IReadOnlyCollection<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string> { "的", "了", "是", "在", "和", "与", "或", "以及", "如何", "怎么", "什么", "为什么" };
        return Regex.Split(text, @"\W+")
            .Where(word => word.Length > 1 && !stopWords.Contains(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string TruncateText(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }

    private static IReadOnlyList<string> SplitText(string text, int chunkLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var parts = new List<string>();
        for (var i = 0; i < text.Length; i += chunkLength)
        {
            parts.Add(text.Substring(i, Math.Min(chunkLength, text.Length - i)));
        }

        return parts;
    }

    private sealed record RetrievalChunk(Guid Id, Guid DocumentId, string DocumentTitle, string Text, int StartIndex, int EndIndex);

    private sealed class ChatSessionRecord
    {
        public Guid Id { get; }
        public string Title { get; set; }
        public List<ChatMessageRecord> Messages { get; }
        public DateTime CreatedAt { get; }
        public DateTime UpdatedAt { get; set; }

        public ChatSessionRecord(Guid id, string title, List<ChatMessageRecord> messages, DateTime createdAt)
        {
            Id = id;
            Title = title;
            Messages = messages;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }
    }

    private sealed record ChatMessageRecord(string Role, string Content)
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }
}
