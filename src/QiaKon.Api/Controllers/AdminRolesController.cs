using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = "Admin")]
public class AdminRolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<AdminRolesController> _logger;

    public AdminRolesController(IRoleService roleService, ILogger<AdminRolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    [HttpGet]
    public ApiResponse<IReadOnlyList<RoleDto>> GetAll()
    {
        var roles = _roleService.GetAll();
        return ApiResponse<IReadOnlyList<RoleDto>>.Ok(roles);
    }

    /// <summary>
    /// 获取角色详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public ApiResponse<RoleDto> GetById(Guid id)
    {
        var role = _roleService.GetById(id);
        return role is null
            ? ApiResponse<RoleDto>.Fail("角色不存在", 404)
            : ApiResponse<RoleDto>.Ok(role);
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    [HttpPost]
    public ApiResponse<RoleDto> Create([FromBody] CreateRoleDto request)
    {
        var role = _roleService.Create(request);
        _logger.LogInformation("角色 {RoleName} 创建成功", request.Name);
        return ApiResponse<RoleDto>.Ok(role, "角色创建成功");
    }

    /// <summary>
    /// 更新角色
    /// </summary>
    [HttpPut("{id:guid}")]
    public ApiResponse<RoleDto> Update(Guid id, [FromBody] UpdateRoleDto request)
    {
        var role = _roleService.Update(id, request);
        return role is null
            ? ApiResponse<RoleDto>.Fail("角色不存在", 404)
            : ApiResponse<RoleDto>.Ok(role, "角色更新成功");
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    [HttpDelete("{id:guid}")]
    public ApiResponse Delete(Guid id)
    {
        var role = _roleService.GetById(id);
        var result = _roleService.Delete(id);
        if (result)
            _logger.LogInformation("角色 {RoleId} 被删除", id);
        return result
            ? ApiResponse.Ok("角色删除成功")
            : ApiResponse.Fail("删除失败，角色可能不存在或为系统保留", 404);
    }

    /// <summary>
    /// 配置角色权限矩阵
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    public ApiResponse<RoleDto> UpdatePermissions(Guid id, [FromBody] PermissionMatrixDto permissions)
    {
        var role = _roleService.UpdatePermissions(id, permissions);
        if (role is null)
            return ApiResponse<RoleDto>.Fail("角色不存在或为系统保留角色", 404);
        _logger.LogInformation("角色 {RoleId} 权限被更新", id);
        return ApiResponse<RoleDto>.Ok(role, "权限更新成功");
    }
}
