using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/admin/departments")]
[Authorize(Roles = "Admin")]
public class AdminDepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly ILogger<AdminDepartmentsController> _logger;

    public AdminDepartmentsController(IDepartmentService departmentService, ILogger<AdminDepartmentsController> logger)
    {
        _departmentService = departmentService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有部门
    /// </summary>
    [HttpGet]
    public ApiResponse<IReadOnlyList<DepartmentDto>> GetAll()
    {
        var departments = _departmentService.GetAll();
        return ApiResponse<IReadOnlyList<DepartmentDto>>.Ok(departments);
    }

    /// <summary>
    /// 获取部门详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public ApiResponse<DepartmentDto> GetById(Guid id)
    {
        var dept = _departmentService.GetById(id);
        return dept is null
            ? ApiResponse<DepartmentDto>.Fail("部门不存在", 404)
            : ApiResponse<DepartmentDto>.Ok(dept);
    }

    /// <summary>
    /// 创建部门
    /// </summary>
    [HttpPost]
    public ApiResponse<DepartmentDto> Create([FromBody] CreateDepartmentDto request)
    {
        var userId = GetCurrentUserId();
        var dept = _departmentService.Create(request, userId);
        _logger.LogInformation("部门 {DeptName} 创建成功 by {Operator}", request.Name, userId);
        return ApiResponse<DepartmentDto>.Ok(dept, "部门创建成功");
    }

    /// <summary>
    /// 更新部门
    /// </summary>
    [HttpPut("{id:guid}")]
    public ApiResponse<DepartmentDto> Update(Guid id, [FromBody] UpdateDepartmentDto request)
    {
        var dept = _departmentService.Update(id, request);
        return dept is null
            ? ApiResponse<DepartmentDto>.Fail("部门不存在", 404)
            : ApiResponse<DepartmentDto>.Ok(dept, "部门更新成功");
    }

    /// <summary>
    /// 删除部门
    /// </summary>
    [HttpDelete("{id:guid}")]
    public ApiResponse Delete(Guid id)
    {
        var result = _departmentService.Delete(id);
        if (result)
            _logger.LogInformation("部门 {DeptId} 被删除", id);
        return result
            ? ApiResponse.Ok("部门删除成功")
            : ApiResponse.Fail("删除失败，部门可能不存在或为系统保留", 404);
    }

    /// <summary>
    /// 获取部门成员
    /// </summary>
    [HttpGet("{id:guid}/members")]
    public ApiResponse<IReadOnlyList<UserListItemDto>> GetMembers(Guid id)
    {
        var dept = _departmentService.GetById(id);
        if (dept is null)
            return ApiResponse<IReadOnlyList<UserListItemDto>>.Fail("部门不存在", 404);

        var members = _departmentService.GetMembers(id);
        return ApiResponse<IReadOnlyList<UserListItemDto>>.Ok(members);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
