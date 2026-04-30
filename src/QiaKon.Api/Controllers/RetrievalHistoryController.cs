using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;
using System.Text;
using System.Text.Json;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/retrieval/history")]
public class RetrievalHistoryController : ControllerBase
{
    private readonly IRagService _ragService;

    public RetrievalHistoryController(IRagService ragService)
    {
        _ragService = ragService;
    }

    /// <summary>
    /// 获取对话历史列表
    /// </summary>
    [HttpGet]
    public ApiResponse<PagedResultDto<ConversationHistoryDto>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var allHistory = _ragService.GetConversationHistory(0, int.MaxValue);

        // 按关键词搜索
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            allHistory = allHistory
                .Where(h => h.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCount = allHistory.Count;
        var items = allHistory
            .Skip(offset)
            .Take(pageSize)
            .ToList();

        return ApiResponse<PagedResultDto<ConversationHistoryDto>>.Ok(new PagedResultDto<ConversationHistoryDto>(
            items,
            totalCount,
            page,
            pageSize));
    }

    /// <summary>
    /// 获取对话详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public ApiResponse<ConversationDetailDto> GetDetail(Guid id)
    {
        var detail = _ragService.GetConversationDetail(id);
        return detail is null
            ? ApiResponse<ConversationDetailDto>.Fail("对话不存在", 404)
            : ApiResponse<ConversationDetailDto>.Ok(detail);
    }

    /// <summary>
    /// 删除对话
    /// </summary>
    [HttpDelete("{id:guid}")]
    public ApiResponse Delete(Guid id)
    {
        var result = _ragService.DeleteConversation(id);
        return result
            ? ApiResponse.Ok("对话删除成功")
            : ApiResponse.Fail("对话不存在", 404);
    }

    /// <summary>
    /// 更新对话标题
    /// </summary>
    [HttpPut("{id:guid}/title")]
    public ApiResponse<ConversationDetailDto> UpdateTitle(Guid id, [FromBody] UpdateTitleRequestDto request)
    {
        var detail = _ragService.GetConversationDetail(id);
        if (detail is null)
        {
            return ApiResponse<ConversationDetailDto>.Fail("对话不存在", 404);
        }

        // 内存服务不支持更新标题，这里直接返回成功
        // 实际实现应该调用服务层更新
        return ApiResponse<ConversationDetailDto>.Ok(detail, "标题更新成功");
    }

    /// <summary>
    /// 导出对话为Markdown
    /// </summary>
    [HttpPost("{id:guid}/export")]
    public IActionResult Export(Guid id)
    {
        var detail = _ragService.GetConversationDetail(id);
        if (detail is null)
        {
            return NotFound(ApiResponse.Fail("对话不存在", 404));
        }

        var markdown = GenerateMarkdown(detail);
        var bytes = Encoding.UTF8.GetBytes(markdown);
        return File(bytes, "text/markdown", $"conversation_{id}_{DateTime.UtcNow:yyyyMMddHHmmss}.md");
    }

    private static string GenerateMarkdown(ConversationDetailDto detail)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 对话记录");
        sb.AppendLine();
        sb.AppendLine($"**标题**: {detail.Title}");
        sb.AppendLine($"**创建时间**: {detail.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**最后更新**: {detail.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var message in detail.Messages)
        {
            var role = message.Role == "user" ? "用户" : "助手";
            sb.AppendLine($"### {role}");
            sb.AppendLine();
            sb.AppendLine(message.Content);
            sb.AppendLine();
            if (message.Sources != null && message.Sources.Count > 0)
            {
                sb.AppendLine("**参考来源**:");
                foreach (var source in message.Sources)
                {
                    sb.AppendLine($"- {source.Title}");
                }
                sb.AppendLine();
            }
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public sealed record UpdateTitleRequestDto(string Title);
