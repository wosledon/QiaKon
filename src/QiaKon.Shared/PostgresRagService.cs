using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
            var modelSelection = ResolveInferenceModel(request);
            var llmRequest = BuildCompletionRequest(modelSelection, request.Query, retrieved.Results, request.EnableThinking);
            responseText = NormalizeAssistantResponse(ExecuteCompletion(modelSelection, llmRequest), request.EnableThinking);
            sources = BuildSources(retrieved.Results);
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

    public async IAsyncEnumerable<RagChatStreamEventDto> StreamChat(
        RagChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
        session.UpdatedAt = userMessage.CreatedAt;
        await _dbContext.SaveChangesAsync(cancellationToken);

        string responseText;
        IReadOnlyList<RagSourceDto> sources;

        if (retrieved.Results.Count == 0)
        {
            responseText = "我没有从当前知识库中检索到足够相关的内容，建议先确认文档已完成解析、分块和索引。";
            sources = Array.Empty<RagSourceDto>();
            yield return new RagChatStreamEventDto("chunk", Delta: responseText);
        }
        else
        {
            var modelSelection = ResolveInferenceModel(request);
            var llmRequest = BuildCompletionRequest(modelSelection, request.Query, retrieved.Results, request.EnableThinking);
            var rawResponseBuilder = new StringBuilder();
            var emittedVisibleLength = 0;

            await foreach (var delta in ExecuteCompletionStream(modelSelection, llmRequest, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(delta))
                {
                    continue;
                }

                rawResponseBuilder.Append(delta);

                var visibleResponse = NormalizeAssistantResponse(rawResponseBuilder.ToString(), request.EnableThinking, streaming: true);
                if (visibleResponse.Length <= emittedVisibleLength)
                {
                    continue;
                }

                var visibleDelta = visibleResponse[emittedVisibleLength..];
                emittedVisibleLength = visibleResponse.Length;

                if (!string.IsNullOrWhiteSpace(visibleDelta))
                {
                    yield return new RagChatStreamEventDto("chunk", Delta: visibleDelta);
                }
            }

            responseText = NormalizeAssistantResponse(rawResponseBuilder.ToString(), request.EnableThinking);
            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new InvalidOperationException($"模型 {modelSelection.Model.Name} 返回了空响应。");
            }

            sources = BuildSources(retrieved.Results);
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        yield return new RagChatStreamEventDto(
            "done",
            Response: responseText,
            Sources: sources,
            ConversationId: conversationId,
            Turns: CountTurns(session.Id));
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

    private ConfiguredLlmModelSelection ResolveInferenceModel(RagChatRequestDto request)
    {
        var modelSelection = request.ModelId.HasValue
            ? _modelResolver.TryGetInferenceModel(request.ModelId.Value)
            : _modelResolver.TryGetDefaultInferenceModel();

        if (modelSelection is null)
        {
            throw new InvalidOperationException("未找到已启用的默认推理模型，请先在大模型管理中启用并设为默认。当前对话链路已切换为真实推理，不再使用模板回答。");
        }

        return modelSelection;
    }

    private ChatCompletionRequest BuildCompletionRequest(
        ConfiguredLlmModelSelection modelSelection,
        string query,
        IReadOnlyList<RetrieveResultItemDto> results,
        bool enableThinking)
        => new()
        {
            Model = modelSelection.Model.ActualModelName,
            Messages =
            [
                ChatMessage.System(BuildSystemPrompt(enableThinking)),
                ChatMessage.User(BuildRagPrompt(query, results, enableThinking))
            ],
            InferenceOptions = new LlmInferenceOptions
            {
                Temperature = 0.2,
                MaxTokens = Math.Min(modelSelection.Model.MaxTokens ?? 4096, 4096)
            }
        };

    private static IReadOnlyList<RagSourceDto> BuildSources(IReadOnlyList<RetrieveResultItemDto> results)
        => results
            .Select(result => new RagSourceDto(
                result.DocumentId,
                result.DocumentTitle,
                result.Text,
                result.Score,
                TruncateText(result.Text, 120)))
            .ToList();

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

    private async IAsyncEnumerable<string> ExecuteCompletionStream(
        ConfiguredLlmModelSelection modelSelection,
        ChatCompletionRequest llmRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var options = _modelResolver.BuildOptions(modelSelection);
        var client = _llmClientFactory.CreateClient(options);

        try
        {
            await foreach (var chunk in client.CompleteStreamAsync(llmRequest, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(chunk.Content))
                {
                    yield return chunk.Content;
                }
            }
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    private static string BuildSystemPrompt(bool enableThinking)
        => enableThinking
            ? "你是 QiaKon 知识库问答助手。请严格基于给定的知识库片段回答，不要编造未检索到的事实；如果证据不足，请明确说明。你可以先使用 꽁...ground 标签输出思考过程，但最终给用户的正式答案必须写在标签之外。"
            : "你是 QiaKon 知识库问答助手。请严格基于给定的知识库片段回答，不要编造未检索到的事实；如果证据不足，请明确说明。不要输出任何 꽁 标签、思考过程、推理链或内部分析，只输出最终回答。";

    private static string BuildRagPrompt(string query, IReadOnlyList<RetrieveResultItemDto> results, bool enableThinking)
    {
        var sb = new StringBuilder();
        sb.AppendLine("请基于以下知识库片段回答用户问题。");
        sb.AppendLine();
        sb.AppendLine("【重要规则】");
        sb.AppendLine("1. 必须先判断：下面的片段是否与用户问题直接相关？");
        sb.AppendLine("2. 如果不相关（主题完全不同），请明确回复：\"当前知识库中没有与问题直接相关的内容，建议上传相关文档后再试。\"不要基于无关片段编造答案。");
        sb.AppendLine("3. 如果部分相关，可以回答，但必须明确说明哪些信息来自知识库，哪些是你的推断。");
        sb.AppendLine("4. 绝对不要编造任何片段中没有提到的事实、数据或结论。");
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
        if (enableThinking)
        {
            sb.AppendLine("请先在 꽁...ground 中简要分析：①这些片段是否与问题相关？②如果相关，哪些部分可以支撑回答？③如果无关，应该如何回复？");
            sb.AppendLine("然后在标签外给出最终答案。如果片段不相关，直接说明知识库中没有相关内容，不要强行回答。");
        }
        else
        {
            sb.AppendLine("如果片段与问题不相关，请直接说明知识库中没有相关内容。如果相关，请基于片段给出准确回答，并引用来源编号（如[1]、[2]）。");
        }

        return sb.ToString();
    }

    private static string NormalizeAssistantResponse(string text, bool enableThinking, bool streaming = false)
    {
        var normalized = enableThinking ? text : StripThinkBlocks(text);
        if (streaming)
        {
            return normalized;
        }

        return normalized.Trim();
    }

    private static string StripThinkBlocks(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var visible = new StringBuilder();
        var cursor = 0;

        while (cursor < text.Length)
        {
            var openIndex = text.IndexOf("꽁", cursor, StringComparison.OrdinalIgnoreCase);
            if (openIndex < 0)
            {
                visible.Append(text[cursor..]);
                break;
            }

            visible.Append(text[cursor..openIndex]);

            var closeIndex = text.IndexOf("地", openIndex + 7, StringComparison.OrdinalIgnoreCase);
            if (closeIndex < 0)
            {
                break;
            }

            cursor = closeIndex + 8;
        }

        var result = Regex.Replace(visible.ToString(), "</?think>", string.Empty, RegexOptions.IgnoreCase);
        return TrimIncompleteThinkTagSuffix(result);
    }

    private static string TrimIncompleteThinkTagSuffix(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var partialTags = new[] { "꽁", "地" };
        foreach (var tag in partialTags)
        {
            for (var length = tag.Length - 1; length > 0; length--)
            {
                var suffix = tag[..length];
                if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return text[..^length];
                }
            }
        }

        return text;
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
