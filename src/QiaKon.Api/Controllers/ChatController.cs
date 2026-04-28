using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;
using System.Text.Json;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api")]
public class ChatController : ControllerBase
{
    private readonly IRagService _ragService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IRagService ragService, ILogger<ChatController> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// RAG检索
    /// </summary>
    [HttpPost("retrieve")]
    [HttpPost("rag/retrieve")]
    public ApiResponse<RetrieveResponseDto> Retrieve([FromBody] RetrieveRequestDto request)
    {
        try
        {
            var result = _ragService.Retrieve(request);
            return ApiResponse<RetrieveResponseDto>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检索失败");
            return ApiResponse<RetrieveResponseDto>.Fail("检索处理失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// RAG问答
    /// </summary>
    [HttpPost("rag/chat")]
    public ApiResponse<RagChatResponseDto> Chat([FromBody] RagChatRequestDto request)
    {
        try
        {
            var result = _ragService.Chat(request);
            _logger.LogInformation("RAG chat query: {Query}, sources: {SourceCount}", request.Query, result.Sources.Count);
            return ApiResponse<RagChatResponseDto>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "问答失败");
            return ApiResponse<RagChatResponseDto>.Fail("问答处理失败: " + ex.Message, 500);
        }
    }

    /// <summary>
    /// 流式RAG问答
    /// </summary>
    [HttpPost("rag/chat/stream")]
    public async Task ChatStream([FromBody] RagChatRequestDto request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            var result = _ragService.Chat(request);

            var payload = JsonSerializer.Serialize(new
            {
                response = result.Response,
                sources = result.Sources,
                conversationId = result.ConversationId,
                turns = result.Turns,
            });

            await Response.WriteAsync($"data: {payload}\n\n");
            await Response.WriteAsync("event: done\ndata: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式问答失败");
            await Response.WriteAsync($"event: error\ndata: {JsonSerializer.Serialize(new { message = ex.Message })}\n\n");
        }
    }
}
