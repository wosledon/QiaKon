using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;
using System.Text.Json;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/retrieval")]
public class ChatController : ControllerBase
{
    private readonly IRagService _ragService;
    private readonly ConfiguredLlmModelResolver _modelResolver;
    private readonly ILogger<ChatController> _logger;

    private static readonly Dictionary<Guid, Guid> _conversationModels = new();

    public ChatController(IRagService ragService, ConfiguredLlmModelResolver modelResolver, ILogger<ChatController> logger)
    {
        _ragService = ragService;
        _modelResolver = modelResolver;
        _logger = logger;
    }

    /// <summary>
    /// RAG检索
    /// </summary>
    [HttpPost("retrieve")]
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
    [HttpPost("chat")]
    public ApiResponse<RagChatResponseDto> Chat([FromBody] RagChatRequestDto request)
    {
        try
        {
            if (!request.ModelId.HasValue
                && request.ConversationId.HasValue
                && _conversationModels.TryGetValue(request.ConversationId.Value, out var selectedModelId))
            {
                request = request with { ModelId = selectedModelId };
            }

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
    [HttpPost("chat/stream")]
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

    /// <summary>
    /// 获取可用的推理模型列表
    /// </summary>
    [HttpGet("models")]
    public ApiResponse<IReadOnlyList<LlmModelDto>> GetModels()
    {
        return ApiResponse<IReadOnlyList<LlmModelDto>>.Ok(_modelResolver.GetEnabledInferenceModels());
    }

    /// <summary>
    /// 切换当前对话使用的模型
    /// </summary>
    [HttpPost("models/switch")]
    public ApiResponse<bool> SwitchModel([FromBody] SwitchModelRequestDto request)
    {
        var model = _modelResolver.GetEnabledInferenceModels().FirstOrDefault(m => m.Id == request.ModelId);
        if (model == null)
        {
            return ApiResponse<bool>.Fail("模型不存在", 404);
        }

        if (!model.IsEnabled)
        {
            return ApiResponse<bool>.Fail("模型未启用", 400);
        }

        if (request.ConversationId.HasValue)
        {
            _conversationModels[request.ConversationId.Value] = request.ModelId;
        }

        return ApiResponse<bool>.Ok(true, $"已切换到模型: {model.Name}");
    }
}

public sealed record SwitchModelRequestDto(Guid ModelId, Guid? ConversationId = null);
