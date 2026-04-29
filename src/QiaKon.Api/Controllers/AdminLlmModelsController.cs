using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/admin/llm-models")]
public class AdminLlmModelsController : ControllerBase
{
    private readonly ILlmProviderService _llmProviderService;

    public AdminLlmModelsController(ILlmProviderService llmProviderService)
    {
        _llmProviderService = llmProviderService;
    }

    /// <summary>
    /// 获取所有LLM提供商
    /// </summary>
    [HttpGet("providers")]
    public ApiResponse<IReadOnlyList<LlmProviderDto>> GetProviders()
    {
        var providers = _llmProviderService.GetAll();
        return ApiResponse<IReadOnlyList<LlmProviderDto>>.Ok(providers);
    }

    /// <summary>
    /// 获取提供商详情
    /// </summary>
    [HttpGet("providers/{id:guid}")]
    public ApiResponse<LlmProviderDto> GetProvider(Guid id)
    {
        var provider = _llmProviderService.GetById(id);
        return provider is null
            ? ApiResponse<LlmProviderDto>.Fail("提供商不存在", 404)
            : ApiResponse<LlmProviderDto>.Ok(provider);
    }

    /// <summary>
    /// 创建LLM提供商
    /// </summary>
    [HttpPost("providers")]
    public ApiResponse<LlmProviderDto> CreateProvider([FromBody] CreateLlmProviderDto request)
    {
        var provider = _llmProviderService.Create(request);
        return ApiResponse<LlmProviderDto>.Ok(provider, "提供商创建成功");
    }

    /// <summary>
    /// 更新LLM提供商
    /// </summary>
    [HttpPut("providers/{id:guid}")]
    public ApiResponse<LlmProviderDto> UpdateProvider(Guid id, [FromBody] CreateLlmProviderDto request)
    {
        var provider = _llmProviderService.Update(id, request);
        return provider is null
            ? ApiResponse<LlmProviderDto>.Fail("提供商不存在", 404)
            : ApiResponse<LlmProviderDto>.Ok(provider, "提供商更新成功");
    }

    /// <summary>
    /// 删除LLM提供商
    /// </summary>
    [HttpDelete("providers/{id:guid}")]
    public ApiResponse DeleteProvider(Guid id)
    {
        var result = _llmProviderService.Delete(id);
        return result
            ? ApiResponse.Ok("提供商删除成功")
            : ApiResponse.Fail("提供商不存在", 404);
    }

    /// <summary>
    /// 测试供应商连接
    /// </summary>
    [HttpPost("providers/{id:guid}/test")]
    public ApiResponse<TestConnectionResultDto> TestProviderConnection(Guid id)
    {
        var (success, message, responseTimeMs) = _llmProviderService.TestConnection(id);
        var result = new TestConnectionResultDto(success, message, responseTimeMs);
        return success
            ? ApiResponse<TestConnectionResultDto>.Ok(result, "连接测试成功")
            : ApiResponse<TestConnectionResultDto>.Fail(message, 400);
    }

    /// <summary>
    /// 获取供应商下的模型列表
    /// </summary>
    [HttpGet("providers/{id:guid}/models")]
    public ApiResponse<IReadOnlyList<LlmModelDto>> GetProviderModels(Guid id)
    {
        var provider = _llmProviderService.GetById(id);
        if (provider is null)
        {
            return ApiResponse<IReadOnlyList<LlmModelDto>>.Fail("提供商不存在", 404);
        }

        var models = _llmProviderService.GetModelsByProviderId(id);
        return ApiResponse<IReadOnlyList<LlmModelDto>>.Ok(models);
    }

    /// <summary>
    /// 添加模型
    /// </summary>
    [HttpPost("models")]
    public ApiResponse<LlmModelDto> AddModel([FromBody] CreateLlmModelDto request)
    {
        var model = _llmProviderService.AddModel(request);
        return model is null
            ? ApiResponse<LlmModelDto>.Fail("提供商不存在", 404)
            : ApiResponse<LlmModelDto>.Ok(model, "模型添加成功");
    }

    /// <summary>
    /// 编辑模型
    /// </summary>
    [HttpPut("models/{modelId:guid}")]
    public ApiResponse<LlmModelDto> UpdateModel(Guid modelId, [FromBody] UpdateLlmModelDto request)
    {
        var model = _llmProviderService.UpdateModel(modelId, request);
        return model is null
            ? ApiResponse<LlmModelDto>.Fail("模型不存在", 404)
            : ApiResponse<LlmModelDto>.Ok(model, "模型更新成功");
    }

    /// <summary>
    /// 删除模型
    /// </summary>
    [HttpDelete("models/{modelId:guid}")]
    public ApiResponse DeleteModel(Guid modelId)
    {
        var result = _llmProviderService.DeleteModel(modelId);
        return result
            ? ApiResponse.Ok("模型删除成功")
            : ApiResponse.Fail("模型不存在或为内置模型", 404);
    }

    /// <summary>
    /// 设置默认模型
    /// </summary>
    [HttpPut("models/{modelId:guid}/set-default")]
    public ApiResponse SetDefaultModel(Guid modelId)
    {
        var result = _llmProviderService.SetDefaultModel(modelId);
        return result
            ? ApiResponse.Ok("已设为默认模型")
            : ApiResponse.Fail("模型不存在", 404);
    }

    /// <summary>
    /// 启用/禁用模型
    /// </summary>
    [HttpPut("models/{modelId:guid}/toggle")]
    public ApiResponse ToggleModel(Guid modelId, [FromQuery] bool enabled = true)
    {
        var result = _llmProviderService.EnableModel(modelId, enabled);
        return result
            ? ApiResponse.Ok(enabled ? "模型已启用" : "模型已禁用")
            : ApiResponse.Fail("模型不存在", 404);
    }

    /// <summary>
    /// 获取内置Embedding模型列表
    /// </summary>
    [HttpGet("embeddings")]
    public ApiResponse<IReadOnlyList<LlmModelDto>> GetBuiltInEmbeddingModels()
    {
        var models = _llmProviderService.GetBuiltInEmbeddingModels();
        return ApiResponse<IReadOnlyList<LlmModelDto>>.Ok(models);
    }
}

public sealed record TestConnectionResultDto(bool Success, string Message, double? ResponseTimeMs);
