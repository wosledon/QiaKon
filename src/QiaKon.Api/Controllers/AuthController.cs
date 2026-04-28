using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    public ApiResponse<LoginResponseDto> Login([FromBody] LoginRequestDto request)
    {
        var result = _authService.Login(request);

        if (result == null)
        {
            _logger.LogWarning("Login failed for user: {Username}", request.Username);
            return ApiResponse<LoginResponseDto>.Fail("用户名或密码错误", 401);
        }

        _logger.LogInformation("User logged in: {Username}", request.Username);
        return ApiResponse<LoginResponseDto>.Ok(result, "登录成功");
    }

    /// <summary>
    /// 刷新Token
    /// </summary>
    [HttpPost("refresh")]
    public ApiResponse<LoginResponseDto> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        // 简化的刷新逻辑 - 实际应该验证refresh token
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return ApiResponse<LoginResponseDto>.Fail("Refresh token is required", 400);
        }

        return ApiResponse<LoginResponseDto>.Fail("Refresh token not supported in this version", 501);
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    [HttpGet("me")]
    public ApiResponse<UserDto> GetCurrentUser()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return ApiResponse<UserDto>.Fail("User not authenticated", 401);
        }

        var user = _authService.GetUserById(userId.Value);
        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("User not found", 404);
        }

        return ApiResponse<UserDto>.Ok(user);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}
