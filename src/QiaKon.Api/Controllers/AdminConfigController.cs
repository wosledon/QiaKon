using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/admin/config")]
public class AdminConfigController : ControllerBase
{
    private readonly ISystemConfigService _configService;

    public AdminConfigController(ISystemConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    [HttpGet]
    public ApiResponse<SystemConfigDto> GetConfig()
    {
        var config = _configService.GetConfig();
        return ApiResponse<SystemConfigDto>.Ok(config);
    }

    /// <summary>
    /// 更新系统配置
    /// </summary>
    [HttpPut]
    public ApiResponse<SystemConfigDto> UpdateConfig([FromBody] UpdateSystemConfigDto request)
    {
        var config = _configService.UpdateConfig(request);
        return ApiResponse<SystemConfigDto>.Ok(config, "配置更新成功");
    }

    /// <summary>
    /// 重置系统配置为默认值
    /// </summary>
    [HttpPost("reset")]
    public ApiResponse<SystemConfigDto> ResetConfig()
    {
        var config = _configService.ResetConfig();
        return ApiResponse<SystemConfigDto>.Ok(config, "配置已重置为默认值");
    }
}
