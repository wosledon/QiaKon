using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts;
using QiaKon.Retrieval;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IRagPipeline _ragPipeline;
    private readonly ILogger<ChatController> _logger;

    private static readonly List<ChatSession> _sessions = new()
    {
        new ChatSession { Id = Guid.NewGuid(), Title = "示例会话", CreatedAt = DateTime.UtcNow }
    };

    private static readonly Dictionary<Guid, List<ChatMessage>> _messages = new();

    public ChatController(IRagPipeline ragPipeline, ILogger<ChatController> logger)
    {
        _ragPipeline = ragPipeline;
        _logger = logger;
    }

    /// <summary>
    /// 单轮问答
    /// </summary>
    [HttpPost]
    public async Task<ApiResponse<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        try
        {
            var results = await _ragPipeline.RetrieveAsync(request.Query, new RetrievalOptions { TopK = 5 });

            var response = new ChatResponse
            {
                Answer = $"[Mock] 这是对 \"{request.Query}\" 的回答。检索到 {results.Count} 个相关文档块。",
                Sources = results.Select(r => new ChatSource
                {
                    Text = r.Chunk.Text,
                    Score = r.Score,
                    DocumentId = r.Chunk.Metadata.GetValueOrDefault("documentId")?.ToString()
                }).ToList(),
                RetrievedAt = DateTime.UtcNow
            };

            return ApiResponse<ChatResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "问答失败");
            return ApiResponse<ChatResponse>.Fail("问答处理失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 流式输出
    /// </summary>
    [HttpPost("stream")]
    public async Task ChatStream([FromBody] ChatRequest request)
    {
        Response.ContentType = "text/event-stream";
        var query = request.Query;

        try
        {
            var results = await _ragPipeline.RetrieveAsync(query, new RetrievalOptions { TopK = 5 });
            var answer = $"[Mock Stream] 这是对 \"{query}\" 的流式回答...";

            foreach (var chunk in answer.Split(' '))
            {
                await Response.WriteAsync(chunk + " ");
                await Response.Body.FlushAsync();
                await Task.Delay(50);
            }

            await Response.WriteAsync("\n[SOURCES]");
            foreach (var result in results.Take(3))
            {
                await Response.WriteAsync($"\n- [{result.Score:F2}] {result.Chunk.Text[..Math.Min(50, result.Chunk.Text.Length)]}...");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式问答失败");
            await Response.WriteAsync($"\n[ERROR] {ex.Message}");
        }
    }

    /// <summary>
    /// 创建会话
    /// </summary>
    [HttpPost("sessions")]
    public ApiResponse<ChatSession> CreateSession([FromBody] CreateSessionRequest request)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            Title = request.Title ?? $"新会话 {DateTime.UtcNow:HH:mm}",
            CreatedAt = DateTime.UtcNow
        };
        _sessions.Add(session);
        _messages[session.Id] = new List<ChatMessage>();

        return ApiResponse<ChatSession>.Ok(session, "会话创建成功");
    }

    /// <summary>
    /// 获取会话列表
    /// </summary>
    [HttpGet("sessions")]
    public ApiResponse<IEnumerable<ChatSession>> GetSessions()
    {
        return ApiResponse<IEnumerable<ChatSession>>.Ok(_sessions.OrderByDescending(s => s.CreatedAt));
    }

    /// <summary>
    /// 获取消息历史
    /// </summary>
    [HttpGet("sessions/{id:guid}/messages")]
    public ApiResponse<IEnumerable<ChatMessage>> GetMessages(Guid id)
    {
        if (!_messages.TryGetValue(id, out var messages))
        {
            messages = new List<ChatMessage>();
            _messages[id] = messages;
        }
        return ApiResponse<IEnumerable<ChatMessage>>.Ok(messages);
    }
}

public record ChatRequest(string Query, Guid? SessionId = null);
public record CreateSessionRequest(string? Title);

public class ChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<ChatSource> Sources { get; set; } = new();
    public DateTime RetrievedAt { get; set; }
}

public class ChatSource
{
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? DocumentId { get; set; }
}

public class ChatSession
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = "user"; // user/assistant
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
