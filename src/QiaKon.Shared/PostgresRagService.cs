using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts.DTOs;
using QiaKon.Llm;
using QiaKon.Retrieval;

namespace QiaKon.Shared;

internal sealed class PostgresRagService : IRagService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly IRagPipeline _ragPipeline;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly ConfiguredLlmModelResolver _modelResolver;
    private readonly ILogger<PostgresRagService>? _logger;

    public PostgresRagService(
        QiaKonAppDbContext dbContext,
        IRagPipeline ragPipeline,
        ILlmClientFactory llmClientFactory,
        ConfiguredLlmModelResolver modelResolver,
        ILogger<PostgresRagService>? logger = null)
    {
        _dbContext = dbContext;
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

        var userMessage = new ConversationMessageRow
        {
            Id = Guid.NewGuid(),
            ConversationId = session.Id,
            Role = "user",
            Content = request.Query,
            SourcesJson = null,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ConversationMessages.Add(userMessage);

        string responseText;
        IReadOnlyList<RagSourceDto> sources;

        if (retrieved.Results.Count == 0)
        {
            responseText = "我没有从当前知识库中检索到足够相关的内容，建议先确认文档已完成解析、分块和索引。";
            sources = Array.Empty<RagSourceDto>();
        }
        else
        {
            var modelSelection = request.ModelId.HasValue
                ? _modelResolver.TryGetInferenceModel(request.ModelId.Value)
                : _modelResolver.TryGetDefaultInferenceModel();

            if (modelSelection is null)
            {
                throw new InvalidOperationException("未找到已启用的默认推理模型，请先在大模型管理中启用并设为默认。当前对话链路已切换为真实推理，不再使用模板回答。");
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

            responseText = ExecuteCompletion(modelSelection, llmRequest);
            sources = retrieved.Results
                .Select(result => new RagSourceDto(
                    result.DocumentId,
                    result.DocumentTitle,
                    result.Text,
                    result.Score,
                    TruncateText(result.Text, 120)))
                .ToList();
        }

        var assistantMessage = new ConversationMessageRow
        {
            Id = Guid.NewGuid(),
            ConversationId = session.Id,
            Role = "assistant",
            Content = responseText,
            SourcesJson = PostgresPersistenceJson.Serialize(sources),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ConversationMessages.Add(assistantMessage);

        if (string.IsNullOrWhiteSpace(session.Title))
        {
            session.Title = TruncateText(request.Query, 24);
        }

        session.UpdatedAt = assistantMessage.CreatedAt;
        _dbContext.SaveChanges();

        return new RagChatResponseDto(responseText, sources, conversationId, CountTurns(session.Id));
    }

    public IReadOnlyList<ConversationHistoryDto> GetConversationHistory(int offset, int limit, Guid? userId = null)
    {
        var query = _dbContext.ConversationSessions.AsNoTracking().AsQueryable();
        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        var sessions = query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();

        var sessionIds = sessions.Select(x => x.Id).ToList();
        var messageCounts = _dbContext.ConversationMessages.AsNoTracking()
            .Where(x => sessionIds.Contains(x.ConversationId))
            .GroupBy(x => x.ConversationId)
            .ToDictionary(x => x.Key, x => x.Count());

        return sessions
            .Select(x => new ConversationHistoryDto(x.Id, x.Title, messageCounts.GetValueOrDefault(x.Id), x.CreatedAt, x.UpdatedAt))
            .ToList();
    }

    public ConversationDetailDto? GetConversationDetail(Guid conversationId)
    {
        var session = _dbContext.ConversationSessions.AsNoTracking().FirstOrDefault(x => x.Id == conversationId);
        if (session is null)
        {
            return null;
        }

        var messages = _dbContext.ConversationMessages.AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .ToList()
            .Select(ToChatMessageDto)
            .ToList();

        return new ConversationDetailDto(session.Id, session.Title, messages, session.CreatedAt, session.UpdatedAt);
    }

    public bool DeleteConversation(Guid conversationId)
    {
        var session = _dbContext.ConversationSessions.FirstOrDefault(x => x.Id == conversationId);
        if (session is null)
        {
            return false;
        }

        var messages = _dbContext.ConversationMessages.Where(x => x.ConversationId == conversationId).ToList();
        if (messages.Count > 0)
        {
            _dbContext.ConversationMessages.RemoveRange(messages);
        }

        _dbContext.ConversationSessions.Remove(session);
        _dbContext.SaveChanges();
        return true;
    }

    public ConversationDetailDto? UpdateConversationTitle(Guid conversationId, string title)
    {
        var session = _dbContext.ConversationSessions.FirstOrDefault(x => x.Id == conversationId);
        if (session is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            session.Title = title.Trim();
            session.UpdatedAt = DateTime.UtcNow;
            _dbContext.SaveChanges();
        }

        return GetConversationDetail(conversationId);
    }

    private ConversationSessionRow GetOrCreateSession(Guid conversationId, string query)
    {
        var session = _dbContext.ConversationSessions.FirstOrDefault(x => x.Id == conversationId);
        if (session is not null)
        {
            return session;
        }

        session = new ConversationSessionRow
        {
            Id = conversationId,
            UserId = null,
            Title = TruncateText(query, 24),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ConversationSessions.Add(session);
        return session;
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

    private ChatMessageDto ToChatMessageDto(ConversationMessageRow row)
        => new(
            row.Id,
            row.Role,
            row.Content,
            row.CreatedAt,
            PostgresPersistenceJson.Deserialize<IReadOnlyList<RagSourceDto>>(row.SourcesJson));

    private int CountTurns(Guid conversationId)
        => Math.Max(1, _dbContext.ConversationMessages.Count(x => x.ConversationId == conversationId) / 2);

    private static string TruncateText(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
}
