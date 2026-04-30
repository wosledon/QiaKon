using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts.DTOs;
using QiaKon.Llm;
using QiaKon.Retrieval;

namespace QiaKon.Shared;

internal sealed class PostgresRagService : IRagService
{
    private readonly IRagPipeline _ragPipeline;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly ConfiguredLlmModelResolver _modelResolver;
    private readonly ILogger<PostgresRagService>? _logger;
    private readonly Dictionary<Guid, ChatSessionRecord> _sessions = new();
    private readonly object _syncRoot = new();

    public PostgresRagService(
        IRagPipeline ragPipeline,
        ILlmClientFactory llmClientFactory,
        ConfiguredLlmModelResolver modelResolver,
        ILogger<PostgresRagService>? logger = null)
    {
        _ragPipeline = ragPipeline;
        _llmClientFactory = llmClientFactory;
        _modelResolver = modelResolver;
        _logger = logger;
    }

    public RetrieveResponseDto Retrieve(RetrieveRequestDto request)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = _ragPipeline
            .RetrieveAsync(
                request.Query,
                new RetrievalOptions
                {
                    TopK = request.TopK,
                    IncludeDocument = true,
                    MinSimilarity = request.MinScore > 0 ? (float)request.MinScore : null
                })
            .GetAwaiter()
            .GetResult();

        stopwatch.Stop();

        var items = results
            .Select(result => new RetrieveResultItemDto(
                result.Chunk.Id,
                result.Chunk.DocumentId,
                result.Document?.Title ?? "未命名文档",
                result.Chunk.Text,
                Math.Round(result.Score, 4),
                result.Chunk.StartIndex,
                result.Chunk.EndIndex))
            .ToList();

        return new RetrieveResponseDto(items, items.Count, (int)stopwatch.ElapsedMilliseconds);
    }

    public RagChatResponseDto Chat(RagChatRequestDto request)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid();
        var retrieved = Retrieve(new RetrieveRequestDto(request.Query, request.TopK));

        var session = GetOrCreateSession(conversationId, request.Query);
        session.Messages.Add(new ChatMessageRecord("user", request.Query, null));

        if (retrieved.Results.Count == 0)
        {
            const string noResultResponse = "我没有从当前知识库中检索到足够相关的内容，建议先确认文档已完成解析、分块和索引。";
            session.Messages.Add(new ChatMessageRecord("assistant", noResultResponse, Array.Empty<RagSourceDto>()));
            session.UpdatedAt = DateTime.UtcNow;
            return new RagChatResponseDto(noResultResponse, Array.Empty<RagSourceDto>(), conversationId, CountTurns(session));
        }

        var modelSelection = request.ModelId.HasValue
            ? _modelResolver.TryGetInferenceModel(request.ModelId.Value)
            : _modelResolver.TryGetDefaultInferenceModel();

        if (modelSelection is null)
        {
            throw new InvalidOperationException("未找到已启用的默认推理模型，请先在大模型管理中启用并设为默认。\n当前对话链路已切换为真实推理，不再使用模板回答。");
        }

        var llmRequest = new ChatCompletionRequest
        {
            Model = modelSelection.Model.ActualModelName,
            Messages =
            [
                ChatMessage.System("你是 QiaKon 知识库问答助手。请严格基于给定的知识库片段回答，不要编造未检索到的事实；如果证据不足，请明确说明。"),
                ChatMessage.User(BuildRagPrompt(request.Query, retrieved.Results))
            ],
            InferenceOptions = new LlmInferenceOptions
            {
                Temperature = 0.2,
                MaxTokens = Math.Min(modelSelection.Model.MaxTokens ?? 4096, 4096)
            }
        };

        var responseText = ExecuteCompletion(modelSelection, llmRequest);
        var sources = retrieved.Results
            .Select(result => new RagSourceDto(
                result.DocumentId,
                result.DocumentTitle,
                result.Text,
                result.Score,
                TruncateText(result.Text, 120)))
            .ToList();

        session.Messages.Add(new ChatMessageRecord("assistant", responseText, sources));
        session.UpdatedAt = DateTime.UtcNow;

        return new RagChatResponseDto(responseText, sources, conversationId, CountTurns(session));
    }

    public IReadOnlyList<ConversationHistoryDto> GetConversationHistory(int offset, int limit, Guid? userId = null)
    {
        lock (_syncRoot)
        {
            return _sessions.Values
                .OrderByDescending(s => s.UpdatedAt)
                .Skip(offset)
                .Take(limit)
                .Select(s => new ConversationHistoryDto(s.Id, s.Title, s.Messages.Count, s.CreatedAt, s.UpdatedAt))
                .ToList();
        }
    }

    public ConversationDetailDto? GetConversationDetail(Guid conversationId)
    {
        lock (_syncRoot)
        {
            if (!_sessions.TryGetValue(conversationId, out var session))
            {
                return null;
            }

            return new ConversationDetailDto(
                session.Id,
                session.Title,
                session.Messages.Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.CreatedAt, m.Sources)).ToList(),
                session.CreatedAt,
                session.UpdatedAt);
        }
    }

    public bool DeleteConversation(Guid conversationId)
    {
        lock (_syncRoot)
        {
            return _sessions.Remove(conversationId);
        }
    }

    private ChatSessionRecord GetOrCreateSession(Guid conversationId, string query)
    {
        lock (_syncRoot)
        {
            if (_sessions.TryGetValue(conversationId, out var existing))
            {
                return existing;
            }

            var created = new ChatSessionRecord(conversationId, TruncateText(query, 24), [], DateTime.UtcNow);
            _sessions[conversationId] = created;
            return created;
        }
    }

    private string ExecuteCompletion(ConfiguredLlmModelSelection modelSelection, ChatCompletionRequest llmRequest)
    {
        var options = _modelResolver.BuildOptions(modelSelection);
        var client = _llmClientFactory.CreateClient(options);

        try
        {
            var response = client.CompleteAsync(llmRequest).GetAwaiter().GetResult();
            var text = response.Message.GetTextContent();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException($"模型 {modelSelection.Model.Name} 返回了空响应。");
            }

            return text.Trim();
        }
        finally
        {
            client.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    private static string BuildRagPrompt(string query, IReadOnlyList<RetrieveResultItemDto> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("以下是从知识库检索到的相关片段，请基于它们回答用户问题。");
        sb.AppendLine("如果片段不足以支持结论，请明确说明“知识库中暂无足够依据”。");
        sb.AppendLine();

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            sb.AppendLine($"[来源 {i + 1}] 标题：{result.DocumentTitle}");
            sb.AppendLine($"相关度：{result.Score:F4}");
            sb.AppendLine(result.Text);
            sb.AppendLine();
        }

        sb.AppendLine($"用户问题：{query}");
        sb.AppendLine("请输出结构化、准确、可追溯的中文回答，并优先引用上面的来源信息。");
        return sb.ToString();
    }

    private static int CountTurns(ChatSessionRecord session)
        => Math.Max(1, session.Messages.Count / 2);

    private static string TruncateText(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    private sealed class ChatSessionRecord
    {
        public Guid Id { get; }
        public string Title { get; }
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

    private sealed record ChatMessageRecord(string Role, string Content, IReadOnlyList<RagSourceDto>? Sources)
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }
}