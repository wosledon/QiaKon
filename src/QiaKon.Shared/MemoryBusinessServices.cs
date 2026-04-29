using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

/// <summary>
/// 内存态Dashboard服务实现
/// </summary>
public sealed class MemoryDashboardService : IDashboardService
{
    private readonly IDocumentService _documentService;
    private readonly IGraphService _graphService;
    private readonly IRagService _ragService;
    private readonly IAuthService _authService;
    private readonly ILogger<MemoryDashboardService>? _logger;

    public MemoryDashboardService(
        IDocumentService documentService,
        IGraphService graphService,
        IRagService ragService,
        IAuthService authService,
        ILogger<MemoryDashboardService>? logger = null)
    {
        _documentService = documentService;
        _graphService = graphService;
        _ragService = ragService;
        _authService = authService;
        _logger = logger;
    }

    public DashboardStatsDto GetStats()
    {
        var docs = _documentService.GetDocuments(1, 5);
        var entities = _graphService.GetEntities(null, 0, 1000);
        var history = _ragService.GetConversationHistory(0, 5);
        var today = DateTime.UtcNow.Date;

        // 模拟活跃用户数 (实际应从审计日志统计)
        var activeUsers = 3;

        return new DashboardStatsDto(
            docs.TotalCount,
            entities.TotalCount,
            history.Count,
            activeUsers,
            docs.Items.Select(d => new RecentDocumentDto(d.Id, d.Title, d.CreatedAt)).ToList(),
            history.Select(h => new RecentChatDto(h.Id, h.Title, h.CreatedAt, h.MessageCount)).ToList(),
            new List<ComponentHealthDto>
            {
                new("API", "Healthy", "服务正常运行", 15.5),
                new("PostgreSQL", "Healthy", "数据库连接正常", 8.2),
                new("Redis", "Healthy", "缓存服务正常", 2.1),
                new("LLM Provider", "Healthy", "模型服务可用", null),
            });
    }
}

/// <summary>
/// 内存态图谱概览服务实现
/// </summary>
public sealed class MemoryGraphOverviewService : IGraphOverviewService
{
    private readonly IGraphService _graphService;
    private readonly ILogger<MemoryGraphOverviewService>? _logger;

    public MemoryGraphOverviewService(IGraphService graphService, ILogger<MemoryGraphOverviewService>? logger = null)
    {
        _graphService = graphService;
        _logger = logger;
    }

    public GraphOverviewDto GetOverview()
    {
        var entities = _graphService.GetEntities(null, 0, 10000);
        var relations = _graphService.GetRelations(0, 10000);

        var entityTypeDist = entities.Items
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var relationTypeDist = relations.Items
            .GroupBy(r => r.Type)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var deptEntities = entities.Items.Count(e => !e.IsPublic);
        var publicEntities = entities.Items.Count(e => e.IsPublic);

        return new GraphOverviewDto(
            entities.TotalCount,
            relations.TotalCount,
            deptEntities,
            publicEntities,
            entityTypeDist,
            relationTypeDist);
    }
}

/// <summary>
/// 内存态部门服务实现
/// </summary>
public sealed class MemoryDepartmentService : IDepartmentService
{
    private readonly Dictionary<Guid, DepartmentRecord> _departments = new();
    private readonly ILogger<MemoryDepartmentService>? _logger;

    public MemoryDepartmentService(ILogger<MemoryDepartmentService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        var root = new DepartmentRecord(Guid.Parse("11111111-1111-1111-1111-111111111111"), "公司总部", null, DateTime.UtcNow.AddYears(-2));
        var deptEngineering = new DepartmentRecord(Guid.Parse("22222222-2222-2222-2222-222222222222"), "研发部", root.Id, DateTime.UtcNow.AddYears(-1));
        var deptSales = new DepartmentRecord(Guid.Parse("33333333-3333-3333-3333-333333333333"), "销售部", root.Id, DateTime.UtcNow.AddMonths(-6));
        var deptHR = new DepartmentRecord(Guid.Parse("44444444-4444-4444-4444-444444444444"), "人力资源部", root.Id, DateTime.UtcNow.AddMonths(-3));
        var frontend = new DepartmentRecord(Guid.Parse("55555555-5555-5555-5555-555555555555"), "前端组", deptEngineering.Id, DateTime.UtcNow.AddMonths(-2));
        var backend = new DepartmentRecord(Guid.Parse("66666666-6666-6666-6666-666666666666"), "后端组", deptEngineering.Id, DateTime.UtcNow.AddMonths(-2));

        _departments[root.Id] = root;
        _departments[deptEngineering.Id] = deptEngineering;
        _departments[deptSales.Id] = deptSales;
        _departments[deptHR.Id] = deptHR;
        _departments[frontend.Id] = frontend;
        _departments[backend.Id] = backend;
    }

    public IReadOnlyList<DepartmentDto> GetAll()
    {
        return _departments.Values
            .OrderBy(d => d.Name)
            .Select(ToDto)
            .ToList();
    }

    public DepartmentDto? GetById(Guid id)
    {
        return _departments.TryGetValue(id, out var dept) ? ToDto(dept) : null;
    }

    public DepartmentDto Create(CreateDepartmentDto request, Guid userId)
    {
        var dept = new DepartmentRecord(Guid.NewGuid(), request.Name, request.ParentId, DateTime.UtcNow);
        _departments[dept.Id] = dept;
        _logger?.LogInformation("Department created: {Id} - {Name}", dept.Id, dept.Name);
        return ToDto(dept);
    }

    public DepartmentDto? Update(Guid id, UpdateDepartmentDto request)
    {
        if (!_departments.TryGetValue(id, out var dept))
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            dept.Name = request.Name;
        if (request.ParentId.HasValue)
            dept.ParentId = request.ParentId;

        return ToDto(dept);
    }

    public bool Delete(Guid id)
    {
        if (!_departments.Remove(id))
            return false;

        // 清除子部门的父引用
        foreach (var dept in _departments.Values.Where(d => d.ParentId == id))
        {
            dept.ParentId = null;
        }
        return true;
    }

    public IReadOnlyList<UserListItemDto> GetMembers(Guid departmentId)
    {
        // 静态返回空列表，实际应关联用户表
        return new List<UserListItemDto>();
    }

    private DepartmentDto ToDto(DepartmentRecord r)
    {
        var parentName = r.ParentId.HasValue && _departments.TryGetValue(r.ParentId.Value, out var parent) ? parent.Name : null;
        var memberCount = 0; // 简化，实际应关联用户表
        return new DepartmentDto(r.Id, r.Name, r.ParentId, parentName, memberCount, r.CreatedAt);
    }

    private class DepartmentRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid? ParentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DepartmentRecord(Guid id, string name, Guid? parentId, DateTime createdAt)
        {
            Id = id;
            Name = name;
            ParentId = parentId;
            CreatedAt = createdAt;
        }
    }
}

/// <summary>
/// 内存态角色服务实现
/// </summary>
public sealed class MemoryRoleService : IRoleService
{
    private readonly Dictionary<Guid, RoleRecord> _roles = new();
    private readonly ILogger<MemoryRoleService>? _logger;

    public MemoryRoleService(ILogger<MemoryRoleService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        var admin = new RoleRecord(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Admin", "系统管理员，拥有所有权限", true, GetAdminPermissions());
        var knowledgeAdmin = new RoleRecord(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "KnowledgeAdmin", "知识管理员，管理文档和图谱", true, GetKnowledgeAdminPermissions());
        var deptManager = new RoleRecord(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "DepartmentManager", "部门经理，管理本部门资源", true, GetDeptManagerPermissions());
        var deptMember = new RoleRecord(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "DepartmentMember", "部门成员，访问本部门资源", true, GetDeptMemberPermissions());
        var guest = new RoleRecord(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Guest", "访客，仅访问公开资源", true, GetGuestPermissions());

        _roles[admin.Id] = admin;
        _roles[knowledgeAdmin.Id] = knowledgeAdmin;
        _roles[deptManager.Id] = deptManager;
        _roles[deptMember.Id] = deptMember;
        _roles[guest.Id] = guest;
    }

    private static PermissionMatrixDto GetAdminPermissions() => new(
        CanReadPublicDocuments: true, CanWritePublicDocuments: true, CanDeletePublicDocuments: true,
        CanReadDepartmentDocuments: true, CanWriteDepartmentDocuments: true, CanDeleteDepartmentDocuments: true,
        CanReadAllDocuments: true, CanWriteAllDocuments: true, CanDeleteAllDocuments: true,
        CanManageUsers: true, CanManageRoles: true, CanManageDepartments: true,
        CanViewAuditLogs: true, CanManageSystemConfig: true);

    private static PermissionMatrixDto GetKnowledgeAdminPermissions() => new(
        CanReadPublicDocuments: true, CanWritePublicDocuments: true, CanDeletePublicDocuments: true,
        CanReadDepartmentDocuments: true, CanWriteDepartmentDocuments: true, CanDeleteDepartmentDocuments: true,
        CanReadAllDocuments: true, CanWriteAllDocuments: true, CanDeleteAllDocuments: true,
        CanManageUsers: false, CanManageRoles: false, CanManageDepartments: false,
        CanViewAuditLogs: true, CanManageSystemConfig: false);

    private static PermissionMatrixDto GetDeptManagerPermissions() => new(
        CanReadPublicDocuments: true, CanWritePublicDocuments: true, CanDeletePublicDocuments: false,
        CanReadDepartmentDocuments: true, CanWriteDepartmentDocuments: true, CanDeleteDepartmentDocuments: true,
        CanReadAllDocuments: false, CanWriteAllDocuments: false, CanDeleteAllDocuments: false,
        CanManageUsers: false, CanManageRoles: false, CanManageDepartments: false,
        CanViewAuditLogs: false, CanManageSystemConfig: false);

    private static PermissionMatrixDto GetDeptMemberPermissions() => new(
        CanReadPublicDocuments: true, CanWritePublicDocuments: false, CanDeletePublicDocuments: false,
        CanReadDepartmentDocuments: true, CanWriteDepartmentDocuments: false, CanDeleteDepartmentDocuments: false,
        CanReadAllDocuments: false, CanWriteAllDocuments: false, CanDeleteAllDocuments: false,
        CanManageUsers: false, CanManageRoles: false, CanManageDepartments: false,
        CanViewAuditLogs: false, CanManageSystemConfig: false);

    private static PermissionMatrixDto GetGuestPermissions() => new(
        CanReadPublicDocuments: true, CanWritePublicDocuments: false, CanDeletePublicDocuments: false,
        CanReadDepartmentDocuments: false, CanWriteDepartmentDocuments: false, CanDeleteDepartmentDocuments: false,
        CanReadAllDocuments: false, CanWriteAllDocuments: false, CanDeleteAllDocuments: false,
        CanManageUsers: false, CanManageRoles: false, CanManageDepartments: false,
        CanViewAuditLogs: false, CanManageSystemConfig: false);

    public IReadOnlyList<RoleDto> GetAll()
    {
        return _roles.Values.Select(ToDto).ToList();
    }

    public RoleDto? GetById(Guid id)
    {
        return _roles.TryGetValue(id, out var role) ? ToDto(role) : null;
    }

    public RoleDto Create(CreateRoleDto request)
    {
        var role = new RoleRecord(Guid.NewGuid(), request.Name, request.Description, false, request.Permissions ?? GetGuestPermissions());
        _roles[role.Id] = role;
        _logger?.LogInformation("Role created: {Id} - {Name}", role.Id, role.Name);
        return ToDto(role);
    }

    public RoleDto? Update(Guid id, UpdateRoleDto request)
    {
        if (!_roles.TryGetValue(id, out var role))
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            role.Name = request.Name;
        if (!string.IsNullOrWhiteSpace(request.Description))
            role.Description = request.Description;
        if (request.Permissions != null)
            role.Permissions = request.Permissions;

        return ToDto(role);
    }

    public bool Delete(Guid id)
    {
        var role = _roles.GetValueOrDefault(id);
        if (role == null || role.IsSystem)
            return false;
        return _roles.Remove(id);
    }

    public RoleDto? UpdatePermissions(Guid id, PermissionMatrixDto permissions)
    {
        if (!_roles.TryGetValue(id, out var role))
            return null;

        if (role.IsSystem)
            return null; // 系统角色不允许修改权限

        role.Permissions = permissions;
        _logger?.LogInformation("Role permissions updated: {Id} - {Name}", role.Id, role.Name);
        return ToDto(role);
    }

    private RoleDto ToDto(RoleRecord r)
    {
        return new RoleDto(r.Id, r.Name, r.Description, r.IsSystem, 0, r.Permissions);
    }

    private class RoleRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsSystem { get; set; }
        public PermissionMatrixDto Permissions { get; set; }
        public RoleRecord(Guid id, string name, string description, bool isSystem, PermissionMatrixDto permissions)
        {
            Id = id;
            Name = name;
            Description = description;
            IsSystem = isSystem;
            Permissions = permissions;
        }
    }
}

/// <summary>
/// 内存态用户服务实现
/// </summary>
public sealed class MemoryUserService : IUserService
{
    private readonly Dictionary<Guid, (UserRecord User, string PasswordHash)> _users = new();
    private readonly IAuthService _authService;
    private readonly IDepartmentService _departmentService;
    private readonly ILogger<MemoryUserService>? _logger;

    public MemoryUserService(
        IAuthService authService,
        IDepartmentService departmentService,
        ILogger<MemoryUserService>? logger = null)
    {
        _authService = authService;
        _departmentService = departmentService;
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        var users = new[]
        {
            (Id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Username: "admin", Email: "admin@qiakon.com", Password: "password123", DeptId: Guid.Parse("44444444-4444-4444-4444-444444444444"), Role: UserRole.Admin),
            (Id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Username: "kb_admin", Email: "kb_admin@qiakon.com", Password: "password123", DeptId: Guid.Parse("22222222-2222-2222-2222-222222222222"), Role: UserRole.KnowledgeAdmin),
            (Id: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Username: "dept_manager", Email: "dept_mgr@qiakon.com", Password: "password123", DeptId: Guid.Parse("22222222-2222-2222-2222-222222222222"), Role: UserRole.DepartmentManager),
            (Id: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Username: "engineer", Email: "engineer@qiakon.com", Password: "password123", DeptId: Guid.Parse("22222222-2222-2222-2222-222222222222"), Role: UserRole.DepartmentMember),
            (Id: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Username: "guest", Email: "guest@qiakon.com", Password: "password123", DeptId: Guid.Parse("33333333-3333-3333-3333-333333333333"), Role: UserRole.Guest),
            (Id: Guid.Parse("11111111-1111-1111-1111-111111111110"), Username: "zhangwei", Email: "zhangwei@qiakon.com", Password: "password123", DeptId: Guid.Parse("22222222-2222-2222-2222-222222222222"), Role: UserRole.DepartmentMember),
            (Id: Guid.Parse("11111111-1111-1111-1111-111111111111"), Username: "lina", Email: "lina@qiakon.com", Password: "password123", DeptId: Guid.Parse("33333333-3333-3333-3333-333333333333"), Role: UserRole.DepartmentManager),
        };

        foreach (var u in users)
        {
            var dept = _departmentService.GetById(u.DeptId);
            _users[u.Id] = (new UserRecord(u.Id, u.Username, u.Email, u.DeptId, dept?.Name ?? "未知部门", u.Role, true, DateTime.UtcNow.AddDays(-new Random().Next(1, 30))), u.Password);
        }
    }

    public UserPagedResultDto GetUsers(int page, int pageSize, Guid? departmentId = null, UserRole? role = null, bool? isActive = null, string? search = null)
    {
        var query = _users.Values.Select(u => u.User).AsEnumerable();

        if (departmentId.HasValue)
            query = query.Where(u => u.DepartmentId == departmentId.Value);
        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Username.Contains(search, StringComparison.OrdinalIgnoreCase) || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItemDto(u.Id, u.Username, u.Email, u.DepartmentId, u.DepartmentName, u.Role, u.IsActive, u.LastLoginAt))
            .ToList();

        return new UserPagedResultDto(items, totalCount, page, pageSize);
    }

    public UserDto? GetById(Guid id)
    {
        return _users.TryGetValue(id, out var entry) ? ToUserDto(entry.User) : null;
    }

    public UserDto Create(CreateUserDto request, Guid createdBy)
    {
        var user = new UserRecord(Guid.NewGuid(), request.Username, request.Email, request.DepartmentId,
            _departmentService.GetById(request.DepartmentId)?.Name ?? "未知部门", request.Role, true, null);
        _users[user.Id] = (user, request.InitialPassword);
        _logger?.LogInformation("User created: {Id} - {Username} by {CreatedBy}", user.Id, user.Username, createdBy);
        return ToUserDto(user);
    }

    public UserDto? Update(Guid id, UpdateUserDto request, Guid modifiedBy)
    {
        if (!_users.TryGetValue(id, out var entry))
            return null;

        var user = entry.User;
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email;
        if (request.DepartmentId.HasValue)
        {
            user.DepartmentId = request.DepartmentId.Value;
            user.DepartmentName = _departmentService.GetById(request.DepartmentId.Value)?.Name ?? "未知部门";
        }
        if (request.Role.HasValue)
            user.Role = request.Role.Value;
        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        return ToUserDto(user);
    }

    public bool Delete(Guid id)
    {
        return _users.Remove(id);
    }

    public bool ResetPassword(Guid id, Guid modifiedBy)
    {
        if (!_users.TryGetValue(id, out var entry))
            return false;
        var (_, pwd) = entry;
        _users[id] = (entry.User, "password123");
        _logger?.LogInformation("Password reset for user {UserId} by {ModifiedBy}", id, modifiedBy);
        return true;
    }

    public bool ChangePassword(Guid userId, ChangePasswordDto request)
    {
        if (!_users.TryGetValue(userId, out var entry))
            return false;
        if (entry.PasswordHash != request.CurrentPassword)
            return false;
        if (request.NewPassword != request.ConfirmPassword)
            return false;

        _users[userId] = (entry.User, request.NewPassword);
        _logger?.LogInformation("Password changed for user {UserId}", userId);
        return true;
    }

    public bool UpdateProfile(Guid userId, UpdateProfileDto request)
    {
        if (!_users.TryGetValue(userId, out var entry))
            return false;
        if (!string.IsNullOrWhiteSpace(request.Email))
            entry.User.Email = request.Email;
        return true;
    }

    public bool ChangeStatus(Guid id, bool isActive, Guid modifiedBy)
    {
        if (!_users.TryGetValue(id, out var entry))
            return false;
        entry.User.IsActive = isActive;
        _logger?.LogInformation("User status changed: {UserId} to {IsActive} by {ModifiedBy}", id, isActive, modifiedBy);
        return true;
    }

    public BatchOperationResultDto BatchOperation(BatchUserOperationDto request, Guid operatedBy)
    {
        var errors = new List<string>();
        var successCount = 0;
        var failureCount = 0;

        foreach (var userId in request.UserIds)
        {
            try
            {
                switch (request.Operation)
                {
                    case BatchUserOperationType.Enable:
                        if (_users.TryGetValue(userId, out var userEnable))
                        {
                            userEnable.User.IsActive = true;
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                            errors.Add($"用户 {userId} 不存在");
                        }
                        break;

                    case BatchUserOperationType.Disable:
                        if (_users.TryGetValue(userId, out var userDisable))
                        {
                            userDisable.User.IsActive = false;
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                            errors.Add($"用户 {userId} 不存在");
                        }
                        break;

                    case BatchUserOperationType.Delete:
                        if (_users.Remove(userId))
                        {
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                            errors.Add($"用户 {userId} 不存在");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                errors.Add($"用户 {userId}: {ex.Message}");
            }
        }

        _logger?.LogInformation("Batch operation {Operation} performed by {OperatedBy}: {SuccessCount} succeeded, {FailureCount} failed",
            request.Operation, operatedBy, successCount, failureCount);

        return new BatchOperationResultDto(successCount, failureCount, errors);
    }

    private UserDto ToUserDto(UserRecord u)
    {
        return new UserDto(u.Id, u.Username, u.Email, u.DepartmentId, u.DepartmentName, u.Role, u.IsActive);
    }

    private class UserRecord
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public UserRecord(Guid id, string username, string email, Guid departmentId, string departmentName, UserRole role, bool isActive, DateTime? lastLoginAt)
        {
            Id = id;
            Username = username;
            Email = email;
            DepartmentId = departmentId;
            DepartmentName = departmentName;
            Role = role;
            IsActive = isActive;
            LastLoginAt = lastLoginAt;
        }
    }
}
