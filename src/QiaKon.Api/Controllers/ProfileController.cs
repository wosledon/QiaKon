using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public ProfileController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    [HttpGet]
    public ApiResponse<UserDto> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return ApiResponse<UserDto>.Fail("用户未认证", 401);
        }

        var user = _authService.GetUserById(userId.Value);
        return user is null
            ? ApiResponse<UserDto>.Fail("用户不存在", 404)
            : ApiResponse<UserDto>.Ok(user);
    }

    /// <summary>
    /// 更新个人资料
    /// </summary>
    [HttpPut]
    public ApiResponse<UserDto> UpdateProfile([FromBody] UpdateProfileDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return ApiResponse<UserDto>.Fail("用户未认证", 401);
        }

        var result = _userService.UpdateProfile(userId.Value, request);
        if (!result)
        {
            return ApiResponse<UserDto>.Fail("更新失败", 500);
        }

        var user = _authService.GetUserById(userId.Value);
        return ApiResponse<UserDto>.Ok(user!, "个人资料更新成功");
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    [HttpPut("password")]
    public ApiResponse ChangePassword([FromBody] ChangePasswordDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return ApiResponse.Fail("用户未认证", 401);
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponse.Fail("新密码与确认密码不匹配", 400);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return ApiResponse.Fail("新密码长度至少6位", 400);
        }

        var result = _userService.ChangePassword(userId.Value, request);
        return result
            ? ApiResponse.Ok("密码修改成功")
            : ApiResponse.Fail("当前密码错误", 401);
    }

    /// <summary>
    /// 退出登录
    /// </summary>
    [HttpPost("logout")]
    public ApiResponse Logout()
    {
        // 在基于 Token 的认证中，服务端不需要做太多事情
        // 客户端会负责清除本地存储的 Token
        // 如果使用服务端会话，可以在这里清理会话
        return ApiResponse.Ok("已退出登录");
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
