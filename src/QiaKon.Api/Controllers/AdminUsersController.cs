using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(IUserService userService, ILogger<AdminUsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户列表
    /// </summary>
    [HttpGet]
    public ApiResponse<UserPagedResultDto> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] UserRole? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null)
    {
        var result = _userService.GetUsers(page, pageSize, departmentId, role, isActive, search);
        return ApiResponse<UserPagedResultDto>.Ok(result);
    }

    /// <summary>
    /// 获取用户详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public ApiResponse<UserDto> GetById(Guid id)
    {
        var user = _userService.GetById(id);
        return user is null
            ? ApiResponse<UserDto>.Fail("用户不存在", 404)
            : ApiResponse<UserDto>.Ok(user);
    }

    /// <summary>
    /// 创建用户
    /// </summary>
    [HttpPost]
    public ApiResponse<UserDto> Create([FromBody] CreateUserDto request)
    {
        var userId = GetCurrentUserId();
        var user = _userService.Create(request, userId);
        _logger.LogInformation("用户 {Username} 创建成功 by {Operator}", request.Username, userId);
        return ApiResponse<UserDto>.Ok(user, "用户创建成功");
    }

    /// <summary>
    /// 更新用户
    /// </summary>
    [HttpPut("{id:guid}")]
    public ApiResponse<UserDto> Update(Guid id, [FromBody] UpdateUserDto request)
    {
        var userId = GetCurrentUserId();
        var user = _userService.Update(id, request, userId);
        return user is null
            ? ApiResponse<UserDto>.Fail("用户不存在", 404)
            : ApiResponse<UserDto>.Ok(user, "用户更新成功");
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    [HttpDelete("{id:guid}")]
    public ApiResponse Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        var user = _userService.GetById(id);
        var result = _userService.Delete(id);
        if (result)
            _logger.LogInformation("用户 {UserId} 被删除 by {Operator}", id, userId);
        return result
            ? ApiResponse.Ok("用户删除成功")
            : ApiResponse.Fail("用户不存在", 404);
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public ApiResponse ResetPassword(Guid id)
    {
        var userId = GetCurrentUserId();
        var result = _userService.ResetPassword(id, userId);
        return result
            ? ApiResponse.Ok("密码已重置为默认值: password123")
            : ApiResponse.Fail("用户不存在", 404);
    }

    /// <summary>
    /// 启用/禁用用户
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public ApiResponse UpdateStatus(Guid id, [FromBody] UpdateUserStatusDto request)
    {
        var userId = GetCurrentUserId();
        var result = _userService.ChangeStatus(id, request.IsActive, userId);
        if (result)
            _logger.LogInformation("用户 {UserId} 状态被设置为 {IsActive} by {Operator}", id, request.IsActive, userId);
        return result
            ? ApiResponse.Ok(request.IsActive ? "用户已启用" : "用户已禁用")
            : ApiResponse.Fail("用户不存在", 404);
    }

    /// <summary>
    /// 批量操作
    /// </summary>
    [HttpPost("batch")]
    public ApiResponse<BatchOperationResultDto> BatchOperation([FromBody] BatchUserOperationDto request)
    {
        var userId = GetCurrentUserId();
        var result = _userService.BatchOperation(request, userId);
        _logger.LogInformation("批量操作 {Operation} 被执行 by {Operator}: 成功 {SuccessCount}, 失败 {FailureCount}",
            request.Operation, userId, result.SuccessCount, result.FailureCount);
        return ApiResponse<BatchOperationResultDto>.Ok(result, $"批量操作完成: 成功 {result.SuccessCount}, 失败 {result.FailureCount}");
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

/// <summary>
/// 更新用户状态请求
/// </summary>
public sealed record UpdateUserStatusDto(bool IsActive);
