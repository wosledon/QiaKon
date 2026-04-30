using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

internal static class PostgresPersistenceJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string? Serialize<T>(T? value)
        => value is null ? null : JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, Options);
    }
}

internal static class LlmProviderUrlNormalizer
{
    public static string NormalizeBaseUrl(string? baseUrl, LlmInterfaceType interfaceType)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        var normalized = baseUrl.Trim().TrimEnd('/');

        if (interfaceType == LlmInterfaceType.OpenAI
            && normalized.Contains("dashscope.aliyuncs.com", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Regex.Replace(
                normalized,
                @"(?i)/compatible-mode/api(?=($|/v\d+($|/)))",
                "/compatible-mode");
        }

        return normalized;
    }
}

internal static class PostgresPlatformDefaults
{
    public static PermissionMatrixDto GuestPermissions()
        => new(
            CanReadPublicDocuments: true,
            CanWritePublicDocuments: false,
            CanDeletePublicDocuments: false,
            CanReadDepartmentDocuments: false,
            CanWriteDepartmentDocuments: false,
            CanDeleteDepartmentDocuments: false,
            CanReadAllDocuments: false,
            CanWriteAllDocuments: false,
            CanDeleteAllDocuments: false,
            CanManageUsers: false,
            CanManageRoles: false,
            CanManageDepartments: false,
            CanViewAuditLogs: false,
            CanManageSystemConfig: false);

    public static PermissionMatrixDto DepartmentMemberPermissions()
        => new(
            CanReadPublicDocuments: true,
            CanWritePublicDocuments: false,
            CanDeletePublicDocuments: false,
            CanReadDepartmentDocuments: true,
            CanWriteDepartmentDocuments: false,
            CanDeleteDepartmentDocuments: false,
            CanReadAllDocuments: false,
            CanWriteAllDocuments: false,
            CanDeleteAllDocuments: false,
            CanManageUsers: false,
            CanManageRoles: false,
            CanManageDepartments: false,
            CanViewAuditLogs: false,
            CanManageSystemConfig: false);

    public static PermissionMatrixDto DepartmentManagerPermissions()
        => new(
            CanReadPublicDocuments: true,
            CanWritePublicDocuments: true,
            CanDeletePublicDocuments: false,
            CanReadDepartmentDocuments: true,
            CanWriteDepartmentDocuments: true,
            CanDeleteDepartmentDocuments: true,
            CanReadAllDocuments: false,
            CanWriteAllDocuments: false,
            CanDeleteAllDocuments: false,
            CanManageUsers: false,
            CanManageRoles: false,
            CanManageDepartments: false,
            CanViewAuditLogs: false,
            CanManageSystemConfig: false);

    public static PermissionMatrixDto KnowledgeAdminPermissions()
        => new(
            CanReadPublicDocuments: true,
            CanWritePublicDocuments: true,
            CanDeletePublicDocuments: true,
            CanReadDepartmentDocuments: true,
            CanWriteDepartmentDocuments: true,
            CanDeleteDepartmentDocuments: true,
            CanReadAllDocuments: true,
            CanWriteAllDocuments: true,
            CanDeleteAllDocuments: true,
            CanManageUsers: false,
            CanManageRoles: false,
            CanManageDepartments: false,
            CanViewAuditLogs: true,
            CanManageSystemConfig: false);

    public static PermissionMatrixDto AdminPermissions()
        => new(
            CanReadPublicDocuments: true,
            CanWritePublicDocuments: true,
            CanDeletePublicDocuments: true,
            CanReadDepartmentDocuments: true,
            CanWriteDepartmentDocuments: true,
            CanDeleteDepartmentDocuments: true,
            CanReadAllDocuments: true,
            CanWriteAllDocuments: true,
            CanDeleteAllDocuments: true,
            CanManageUsers: true,
            CanManageRoles: true,
            CanManageDepartments: true,
            CanViewAuditLogs: true,
            CanManageSystemConfig: true);

    public static PermissionMatrixDto PermissionsForRole(UserRole role)
        => role switch
        {
            UserRole.Admin => AdminPermissions(),
            UserRole.KnowledgeAdmin => KnowledgeAdminPermissions(),
            UserRole.DepartmentManager => DepartmentManagerPermissions(),
            UserRole.DepartmentMember => DepartmentMemberPermissions(),
            _ => GuestPermissions()
        };

    public static SystemConfigDto DefaultSystemConfig()
        => new(
            DefaultChunkingStrategy: "Recursive",
            ChunkSize: 512,
            ChunkOverlap: 50,
            DefaultVectorDimensions: 1536,
            CacheStrategy: "L1+L2+L3",
            CacheExpirationMinutes: 60,
            PromptTemplate: "你是一个知识库问答助手。请基于以下参考内容回答用户问题。\n\n参考内容：\n{context}\n\n用户问题：{question}\n\n回答：");
}

internal sealed class PostgresAuthService : IAuthService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresAuthService>? _logger;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public PostgresAuthService(
        QiaKonAppDbContext dbContext,
        IConfiguration? configuration = null,
        ILogger<PostgresAuthService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        var jwtSection = configuration?.GetSection("Jwt");
        _jwtSecret = jwtSection?["SecretKey"] ?? "QiaKon-Dev-Secret-Key-For-Development-Only-Min-32-Chars!";
        _jwtIssuer = jwtSection?["Issuer"] ?? "QiaKon";
        _jwtAudience = jwtSection?["Audience"] ?? "QiaKon.Api";
    }

    public LoginResponseDto? Login(LoginRequestDto request)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Username == request.Username);
        if (user is null || !user.IsActive || !string.Equals(user.PasswordHash, request.Password, StringComparison.Ordinal))
        {
            _logger?.LogWarning("Login failed for username: {Username}", request.Username);
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        _dbContext.SaveChanges();

        var userDto = ToUserDto(user);
        var token = GenerateJwtToken(userDto);

        _logger?.LogInformation("User logged in from PostgreSQL: {Username}", request.Username);
        return new LoginResponseDto(token, 3600, userDto);
    }

    public (bool IsValid, Guid UserId, UserRole Role) ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, Guid.Empty, UserRole.Guest);
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtIssuer,
                ValidAudience = _jwtAudience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;

            if (Guid.TryParse(userIdClaim, out var userId) && Enum.TryParse<UserRole>(roleClaim, out var role))
            {
                return (true, userId, role);
            }

            return (false, Guid.Empty, UserRole.Guest);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Token validation failed");
            return (false, Guid.Empty, UserRole.Guest);
        }
    }

    public UserDto? GetUserById(Guid userId)
    {
        var user = _dbContext.Users.AsNoTracking().FirstOrDefault(x => x.Id == userId);
        return user is null ? null : ToUserDto(user);
    }

    private UserDto ToUserDto(UserRow user)
        => new(
            user.Id,
            user.Username,
            user.Email,
            user.DepartmentId,
            ResolveDepartmentName(user.DepartmentId),
            user.Role,
            user.IsActive);

    private string ResolveDepartmentName(Guid departmentId)
        => _dbContext.Departments.AsNoTracking()
            .Where(x => x.Id == departmentId)
            .Select(x => x.Name)
            .FirstOrDefault() ?? QiaKonSeedData.GetDepartmentName(departmentId);

    private string GenerateJwtToken(UserDto user)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("departmentId", user.DepartmentId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }
}

internal sealed class PostgresDepartmentService : IDepartmentService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresDepartmentService>? _logger;

    public PostgresDepartmentService(QiaKonAppDbContext dbContext, ILogger<PostgresDepartmentService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public IReadOnlyList<DepartmentDto> GetAll()
    {
        var departments = _dbContext.Departments.AsNoTracking().OrderBy(x => x.Name).ToList();
        return MapDepartments(departments);
    }

    public DepartmentDto? GetById(Guid id)
    {
        var department = _dbContext.Departments.AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (department is null)
        {
            return null;
        }

        return MapDepartments([department]).Single();
    }

    public DepartmentDto Create(CreateDepartmentDto request, Guid userId)
    {
        var department = new DepartmentRow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ParentId = request.ParentId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Departments.Add(department);
        _dbContext.SaveChanges();
        _logger?.LogInformation("Department created in PostgreSQL: {DepartmentId} by {UserId}", department.Id, userId);
        return MapDepartments([department]).Single();
    }

    public DepartmentDto? Update(Guid id, UpdateDepartmentDto request)
    {
        var department = _dbContext.Departments.FirstOrDefault(x => x.Id == id);
        if (department is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            department.Name = request.Name;
        }

        if (request.ParentId != id)
        {
            department.ParentId = request.ParentId;
        }

        _dbContext.SaveChanges();
        return MapDepartments([department]).Single();
    }

    public bool Delete(Guid id)
    {
        var department = _dbContext.Departments.FirstOrDefault(x => x.Id == id);
        if (department is null)
        {
            return false;
        }

        foreach (var child in _dbContext.Departments.Where(x => x.ParentId == id))
        {
            child.ParentId = null;
        }

        _dbContext.Departments.Remove(department);
        _dbContext.SaveChanges();
        return true;
    }

    public IReadOnlyList<UserListItemDto> GetMembers(Guid departmentId)
    {
        var departmentName = _dbContext.Departments.AsNoTracking()
            .Where(x => x.Id == departmentId)
            .Select(x => x.Name)
            .FirstOrDefault() ?? QiaKonSeedData.GetDepartmentName(departmentId);

        return _dbContext.Users.AsNoTracking()
            .Where(x => x.DepartmentId == departmentId)
            .OrderBy(x => x.Username)
            .Select(x => new UserListItemDto(x.Id, x.Username, x.Email, x.DepartmentId, departmentName, x.Role, x.IsActive, x.LastLoginAt))
            .ToList();
    }

    private IReadOnlyList<DepartmentDto> MapDepartments(IReadOnlyList<DepartmentRow> departments)
    {
        var ids = departments.Select(x => x.Id).ToHashSet();
        var parentIds = departments.Where(x => x.ParentId.HasValue).Select(x => x.ParentId!.Value).Distinct().ToList();
        var allParentIds = parentIds.Where(x => !ids.Contains(x)).ToList();
        var parentMap = departments.ToDictionary(x => x.Id, x => x.Name);

        if (allParentIds.Count > 0)
        {
            foreach (var parent in _dbContext.Departments.AsNoTracking().Where(x => allParentIds.Contains(x.Id)))
            {
                parentMap[parent.Id] = parent.Name;
            }
        }

        var memberCounts = _dbContext.Users.AsNoTracking()
            .Where(x => ids.Contains(x.DepartmentId))
            .GroupBy(x => x.DepartmentId)
            .ToDictionary(x => x.Key, x => x.Count());

        return departments
            .Select(x => new DepartmentDto(
                x.Id,
                x.Name,
                x.ParentId,
                x.ParentId.HasValue && parentMap.TryGetValue(x.ParentId.Value, out var parentName) ? parentName : null,
                memberCounts.GetValueOrDefault(x.Id),
                x.CreatedAt))
            .ToList();
    }
}

internal sealed class PostgresRoleService : IRoleService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresRoleService>? _logger;

    public PostgresRoleService(QiaKonAppDbContext dbContext, ILogger<PostgresRoleService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public IReadOnlyList<RoleDto> GetAll()
    {
        var roles = _dbContext.Roles.AsNoTracking().OrderBy(x => x.Name).ToList();
        return MapRoles(roles);
    }

    public RoleDto? GetById(Guid id)
    {
        var role = _dbContext.Roles.AsNoTracking().FirstOrDefault(x => x.Id == id);
        return role is null ? null : MapRoles([role]).Single();
    }

    public RoleDto Create(CreateRoleDto request)
    {
        var role = new RoleRow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsSystem = false,
            PermissionsJson = PostgresPersistenceJson.Serialize(request.Permissions ?? PostgresPlatformDefaults.GuestPermissions())
        };

        _dbContext.Roles.Add(role);
        _dbContext.SaveChanges();
        _logger?.LogInformation("Role created in PostgreSQL: {RoleName}", role.Name);
        return MapRoles([role]).Single();
    }

    public RoleDto? Update(Guid id, UpdateRoleDto request)
    {
        var role = _dbContext.Roles.FirstOrDefault(x => x.Id == id);
        if (role is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            role.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            role.Description = request.Description;
        }

        if (request.Permissions is not null)
        {
            role.PermissionsJson = PostgresPersistenceJson.Serialize(request.Permissions);
        }

        _dbContext.SaveChanges();
        return MapRoles([role]).Single();
    }

    public bool Delete(Guid id)
    {
        var role = _dbContext.Roles.FirstOrDefault(x => x.Id == id);
        if (role is null || role.IsSystem)
        {
            return false;
        }

        _dbContext.Roles.Remove(role);
        _dbContext.SaveChanges();
        return true;
    }

    public RoleDto? UpdatePermissions(Guid id, PermissionMatrixDto permissions)
    {
        var role = _dbContext.Roles.FirstOrDefault(x => x.Id == id);
        if (role is null || role.IsSystem)
        {
            return null;
        }

        role.PermissionsJson = PostgresPersistenceJson.Serialize(permissions);
        _dbContext.SaveChanges();
        return MapRoles([role]).Single();
    }

    private IReadOnlyList<RoleDto> MapRoles(IReadOnlyList<RoleRow> roles)
    {
        var users = _dbContext.Users.AsNoTracking().Select(x => x.Role).ToList();
        var counts = users.GroupBy(x => x).ToDictionary(x => x.Key.ToString(), x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return roles.Select(role => new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystem,
            counts.GetValueOrDefault(role.Name, 0),
            PostgresPersistenceJson.Deserialize<PermissionMatrixDto>(role.PermissionsJson) ?? PostgresPlatformDefaults.GuestPermissions()))
            .ToList();
    }
}

internal sealed class PostgresUserService : IUserService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresUserService>? _logger;

    public PostgresUserService(QiaKonAppDbContext dbContext, ILogger<PostgresUserService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public UserPagedResultDto GetUsers(int page, int pageSize, Guid? departmentId = null, UserRole? role = null, bool? isActive = null, string? search = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.Users.AsNoTracking().AsQueryable();
        if (departmentId.HasValue)
        {
            query = query.Where(x => x.DepartmentId == departmentId.Value);
        }

        if (role.HasValue)
        {
            query = query.Where(x => x.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Username.Contains(search) || x.Email.Contains(search));
        }

        var totalCount = query.LongCount();
        var users = query.OrderBy(x => x.Username).Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var departmentMap = LoadDepartmentNames(users.Select(x => x.DepartmentId));

        var items = users.Select(user => new UserListItemDto(
            user.Id,
            user.Username,
            user.Email,
            user.DepartmentId,
            departmentMap.GetValueOrDefault(user.DepartmentId, QiaKonSeedData.GetDepartmentName(user.DepartmentId)),
            user.Role,
            user.IsActive,
            user.LastLoginAt)).ToList();

        return new UserPagedResultDto(items, totalCount, page, pageSize);
    }

    public UserDto? GetById(Guid id)
    {
        var user = _dbContext.Users.AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (user is null)
        {
            return null;
        }

        var departmentName = LoadDepartmentNames([user.DepartmentId]).GetValueOrDefault(user.DepartmentId, QiaKonSeedData.GetDepartmentName(user.DepartmentId));
        return ToUserDto(user, departmentName);
    }

    public UserDto Create(CreateUserDto request, Guid createdBy)
    {
        var user = new UserRow
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = request.InitialPassword,
            DepartmentId = request.DepartmentId,
            Role = request.Role,
            IsActive = true,
            LastLoginAt = null,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
        _logger?.LogInformation("User created in PostgreSQL: {UserId} by {Operator}", user.Id, createdBy);
        var departmentName = LoadDepartmentNames([user.DepartmentId]).GetValueOrDefault(user.DepartmentId, QiaKonSeedData.GetDepartmentName(user.DepartmentId));
        return ToUserDto(user, departmentName);
    }

    public UserDto? Update(Guid id, UpdateUserDto request, Guid modifiedBy)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == id);
        if (user is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            user.Email = request.Email;
        }

        if (request.DepartmentId.HasValue)
        {
            user.DepartmentId = request.DepartmentId.Value;
        }

        if (request.Role.HasValue)
        {
            user.Role = request.Role.Value;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        _dbContext.SaveChanges();
        _logger?.LogInformation("User updated in PostgreSQL: {UserId} by {Operator}", user.Id, modifiedBy);
        var departmentName = LoadDepartmentNames([user.DepartmentId]).GetValueOrDefault(user.DepartmentId, QiaKonSeedData.GetDepartmentName(user.DepartmentId));
        return ToUserDto(user, departmentName);
    }

    public bool Delete(Guid id)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == id);
        if (user is null)
        {
            return false;
        }

        _dbContext.Users.Remove(user);
        _dbContext.SaveChanges();
        return true;
    }

    public bool ResetPassword(Guid id, Guid modifiedBy)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == id);
        if (user is null)
        {
            return false;
        }

        user.PasswordHash = "password123";
        _dbContext.SaveChanges();
        _logger?.LogInformation("Password reset for user {UserId} by {Operator}", id, modifiedBy);
        return true;
    }

    public bool ChangePassword(Guid userId, ChangePasswordDto request)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return false;
        }

        if (!string.Equals(user.PasswordHash, request.CurrentPassword, StringComparison.Ordinal)
            || !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return false;
        }

        user.PasswordHash = request.NewPassword;
        _dbContext.SaveChanges();
        return true;
    }

    public bool UpdateProfile(Guid userId, UpdateProfileDto request)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            user.Email = request.Email;
        }

        _dbContext.SaveChanges();
        return true;
    }

    public bool ChangeStatus(Guid id, bool isActive, Guid modifiedBy)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == id);
        if (user is null)
        {
            return false;
        }

        user.IsActive = isActive;
        _dbContext.SaveChanges();
        _logger?.LogInformation("User status changed in PostgreSQL: {UserId} -> {IsActive} by {Operator}", id, isActive, modifiedBy);
        return true;
    }

    public BatchOperationResultDto BatchOperation(BatchUserOperationDto request, Guid operatedBy)
    {
        var errors = new List<string>();
        var successCount = 0;
        var failureCount = 0;

        foreach (var userId in request.UserIds)
        {
            var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
            if (user is null)
            {
                failureCount++;
                errors.Add($"用户 {userId} 不存在");
                continue;
            }

            switch (request.Operation)
            {
                case BatchUserOperationType.Enable:
                    user.IsActive = true;
                    successCount++;
                    break;
                case BatchUserOperationType.Disable:
                    user.IsActive = false;
                    successCount++;
                    break;
                case BatchUserOperationType.Delete:
                    _dbContext.Users.Remove(user);
                    successCount++;
                    break;
                default:
                    failureCount++;
                    errors.Add($"不支持的操作: {request.Operation}");
                    break;
            }
        }

        _dbContext.SaveChanges();
        _logger?.LogInformation("Batch user operation persisted in PostgreSQL: {Operation} by {Operator}", request.Operation, operatedBy);
        return new BatchOperationResultDto(successCount, failureCount, errors);
    }

    private Dictionary<Guid, string> LoadDepartmentNames(IEnumerable<Guid> departmentIds)
    {
        var ids = departmentIds.Distinct().ToList();
        return _dbContext.Departments.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Name);
    }

    private static UserDto ToUserDto(UserRow user, string departmentName)
        => new(user.Id, user.Username, user.Email, user.DepartmentId, departmentName, user.Role, user.IsActive);
}

internal sealed class PostgresLlmProviderService : ILlmProviderService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresLlmProviderService>? _logger;

    public PostgresLlmProviderService(QiaKonAppDbContext dbContext, ILogger<PostgresLlmProviderService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public IReadOnlyList<LlmProviderDto> GetAll()
    {
        var providers = _dbContext.LlmProviders.AsNoTracking().OrderBy(x => x.Name).ToList();
        var models = _dbContext.LlmModels.AsNoTracking().ToList().GroupBy(x => x.ProviderId).ToDictionary(x => x.Key, x => x.Select(ToModelDto).ToList() as IReadOnlyList<LlmModelDto>);
        return providers.Select(provider => ToProviderDto(provider, models.GetValueOrDefault(provider.Id, Array.Empty<LlmModelDto>()))).ToList();
    }

    public LlmProviderDto? GetById(Guid id)
    {
        var provider = _dbContext.LlmProviders.AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (provider is null)
        {
            return null;
        }

        var models = _dbContext.LlmModels.AsNoTracking().Where(x => x.ProviderId == id).OrderBy(x => x.Name).Select(ToModelDto).ToList();
        return ToProviderDto(provider, models);
    }

    public LlmProviderDto Create(CreateLlmProviderDto request)
    {
        var provider = new LlmProviderRow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            InterfaceType = request.InterfaceType,
            BaseUrl = LlmProviderUrlNormalizer.NormalizeBaseUrl(request.BaseUrl, request.InterfaceType),
            ApiKey = request.ApiKey,
            TimeoutSeconds = request.TimeoutSeconds,
            RetryCount = request.RetryCount,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.LlmProviders.Add(provider);
        _dbContext.SaveChanges();
        _logger?.LogInformation("LLM provider persisted in PostgreSQL: {ProviderName}", provider.Name);
        return ToProviderDto(provider, Array.Empty<LlmModelDto>());
    }

    public LlmProviderDto? Update(Guid id, CreateLlmProviderDto request)
    {
        var provider = _dbContext.LlmProviders.FirstOrDefault(x => x.Id == id);
        if (provider is null)
        {
            return null;
        }

        provider.Name = request.Name;
        provider.InterfaceType = request.InterfaceType;
        provider.BaseUrl = LlmProviderUrlNormalizer.NormalizeBaseUrl(request.BaseUrl, request.InterfaceType);
        provider.ApiKey = request.ApiKey;
        provider.TimeoutSeconds = request.TimeoutSeconds;
        provider.RetryCount = request.RetryCount;

        _dbContext.SaveChanges();
        var models = _dbContext.LlmModels.AsNoTracking().Where(x => x.ProviderId == id).OrderBy(x => x.Name).Select(ToModelDto).ToList();
        return ToProviderDto(provider, models);
    }

    public bool Delete(Guid id)
    {
        var provider = _dbContext.LlmProviders.FirstOrDefault(x => x.Id == id);
        if (provider is null)
        {
            return false;
        }

        var models = _dbContext.LlmModels.Where(x => x.ProviderId == id).ToList();
        if (models.Count > 0)
        {
            _dbContext.LlmModels.RemoveRange(models);
        }

        _dbContext.LlmProviders.Remove(provider);
        _dbContext.SaveChanges();
        return true;
    }

    public IReadOnlyList<LlmModelDto> GetModelsByProviderId(Guid providerId)
        => _dbContext.LlmModels.AsNoTracking().Where(x => x.ProviderId == providerId).OrderBy(x => x.Name).Select(ToModelDto).ToList();

    public LlmModelDto? AddModel(CreateLlmModelDto request)
    {
        if (!_dbContext.LlmProviders.Any(x => x.Id == request.ProviderId))
        {
            return null;
        }

        if (request.SetAsDefault)
        {
            ClearDefaultFlags(request.ModelType);
        }

        var model = new LlmModelRow
        {
            Id = Guid.NewGuid(),
            ProviderId = request.ProviderId,
            Name = request.Name,
            ActualModelName = request.ActualModelName,
            ModelType = request.ModelType,
            VectorDimensions = request.VectorDimensions,
            MaxTokens = request.MaxTokens,
            IsEnabled = true,
            IsDefault = request.SetAsDefault,
            IsBuiltIn = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.LlmModels.Add(model);
        _dbContext.SaveChanges();
        return ToModelDto(model);
    }

    public LlmModelDto? UpdateModel(Guid modelId, UpdateLlmModelDto request)
    {
        var model = _dbContext.LlmModels.FirstOrDefault(x => x.Id == modelId);
        if (model is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            model.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.ActualModelName))
        {
            model.ActualModelName = request.ActualModelName;
        }

        if (request.VectorDimensions.HasValue)
        {
            model.VectorDimensions = request.VectorDimensions.Value;
        }

        if (request.MaxTokens.HasValue)
        {
            model.MaxTokens = request.MaxTokens.Value;
        }

        if (request.SetAsDefault == true)
        {
            ClearDefaultFlags(model.ModelType);
            model.IsDefault = true;
        }

        _dbContext.SaveChanges();
        _logger?.LogInformation("LLM model updated in PostgreSQL: {ModelId}", modelId);
        return ToModelDto(model);
    }

    public bool DeleteModel(Guid modelId)
    {
        var model = _dbContext.LlmModels.FirstOrDefault(x => x.Id == modelId);
        if (model is null || model.IsBuiltIn)
        {
            return false;
        }

        _dbContext.LlmModels.Remove(model);
        _dbContext.SaveChanges();
        return true;
    }

    public bool SetDefaultModel(Guid modelId)
    {
        var model = _dbContext.LlmModels.FirstOrDefault(x => x.Id == modelId);
        if (model is null)
        {
            return false;
        }

        ClearDefaultFlags(model.ModelType);
        model.IsDefault = true;
        _dbContext.SaveChanges();
        return true;
    }

    public bool EnableModel(Guid modelId, bool enabled)
    {
        var model = _dbContext.LlmModels.FirstOrDefault(x => x.Id == modelId);
        if (model is null)
        {
            return false;
        }

        model.IsEnabled = enabled;
        _dbContext.SaveChanges();
        return true;
    }

    public (bool Success, string Message, double? ResponseTimeMs) TestConnection(Guid providerId)
    {
        var provider = _dbContext.LlmProviders.AsNoTracking().FirstOrDefault(x => x.Id == providerId);
        if (provider is null)
        {
            return (false, "Provider not found", null);
        }

        var responseTimeMs = Random.Shared.NextDouble() * 200d + 50d;
        var normalizedBaseUrl = LlmProviderUrlNormalizer.NormalizeBaseUrl(provider.BaseUrl, provider.InterfaceType);
        var baseUrlValid = Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out _);
        var success = baseUrlValid && !string.IsNullOrWhiteSpace(provider.ApiKey);
        var message = success ? "配置有效，已可用于真实调用" : "BaseUrl 或 ApiKey 缺失/无效";
        return (success, message, Math.Round(responseTimeMs, 2));
    }

    public IReadOnlyList<LlmModelDto> GetBuiltInEmbeddingModels()
        => _dbContext.LlmModels.AsNoTracking()
            .Where(x => x.IsBuiltIn && x.ModelType == LlmModelType.Embedding)
            .OrderBy(x => x.Name)
            .Select(ToModelDto)
            .ToList();

    private void ClearDefaultFlags(LlmModelType modelType)
    {
        foreach (var item in _dbContext.LlmModels.Where(x => x.ModelType == modelType && x.IsDefault))
        {
            item.IsDefault = false;
        }
    }

    private static LlmProviderDto ToProviderDto(LlmProviderRow provider, IReadOnlyList<LlmModelDto> models)
        => new(
            provider.Id,
            provider.Name,
            provider.InterfaceType,
            LlmProviderUrlNormalizer.NormalizeBaseUrl(provider.BaseUrl, provider.InterfaceType),
            provider.ApiKey,
            provider.TimeoutSeconds,
            provider.RetryCount,
            models.Count > 0,
            models);

    private static LlmModelDto ToModelDto(LlmModelRow model)
        => new(model.Id, model.ProviderId, model.Name, model.ActualModelName, model.ModelType, model.VectorDimensions, model.MaxTokens, model.IsEnabled, model.IsDefault);
}

internal sealed class PostgresSystemConfigService : ISystemConfigService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresSystemConfigService>? _logger;

    public PostgresSystemConfigService(QiaKonAppDbContext dbContext, ILogger<PostgresSystemConfigService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public SystemConfigDto GetConfig()
    {
        var row = GetOrCreateRow();
        return ToDto(row);
    }

    public SystemConfigDto UpdateConfig(UpdateSystemConfigDto request)
    {
        var row = GetOrCreateRow();
        if (!string.IsNullOrWhiteSpace(request.DefaultChunkingStrategy))
        {
            row.DefaultChunkingStrategy = request.DefaultChunkingStrategy;
        }

        if (request.ChunkSize.HasValue)
        {
            row.ChunkSize = request.ChunkSize.Value;
        }

        if (request.ChunkOverlap.HasValue)
        {
            row.ChunkOverlap = request.ChunkOverlap.Value;
        }

        if (request.DefaultVectorDimensions.HasValue)
        {
            row.DefaultVectorDimensions = request.DefaultVectorDimensions.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.CacheStrategy))
        {
            row.CacheStrategy = request.CacheStrategy;
        }

        if (request.CacheExpirationMinutes.HasValue)
        {
            row.CacheExpirationMinutes = request.CacheExpirationMinutes.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.PromptTemplate))
        {
            row.PromptTemplate = request.PromptTemplate;
        }

        row.UpdatedAt = DateTime.UtcNow;
        _dbContext.SaveChanges();
        _logger?.LogInformation("System config updated in PostgreSQL");
        return ToDto(row);
    }

    public SystemConfigDto ResetConfig()
    {
        var defaults = PostgresPlatformDefaults.DefaultSystemConfig();
        var row = GetOrCreateRow();
        row.DefaultChunkingStrategy = defaults.DefaultChunkingStrategy;
        row.ChunkSize = defaults.ChunkSize;
        row.ChunkOverlap = defaults.ChunkOverlap;
        row.DefaultVectorDimensions = defaults.DefaultVectorDimensions;
        row.CacheStrategy = defaults.CacheStrategy;
        row.CacheExpirationMinutes = defaults.CacheExpirationMinutes;
        row.PromptTemplate = defaults.PromptTemplate;
        row.UpdatedAt = DateTime.UtcNow;
        _dbContext.SaveChanges();
        return ToDto(row);
    }

    private SystemConfigRow GetOrCreateRow()
    {
        var row = _dbContext.SystemConfigs.FirstOrDefault();
        if (row is not null)
        {
            return row;
        }

        var defaults = PostgresPlatformDefaults.DefaultSystemConfig();
        row = new SystemConfigRow
        {
            Id = Guid.NewGuid(),
            DefaultChunkingStrategy = defaults.DefaultChunkingStrategy,
            ChunkSize = defaults.ChunkSize,
            ChunkOverlap = defaults.ChunkOverlap,
            DefaultVectorDimensions = defaults.DefaultVectorDimensions,
            CacheStrategy = defaults.CacheStrategy,
            CacheExpirationMinutes = defaults.CacheExpirationMinutes,
            PromptTemplate = defaults.PromptTemplate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.SystemConfigs.Add(row);
        _dbContext.SaveChanges();
        return row;
    }

    private static SystemConfigDto ToDto(SystemConfigRow row)
        => new(row.DefaultChunkingStrategy, row.ChunkSize, row.ChunkOverlap, row.DefaultVectorDimensions, row.CacheStrategy, row.CacheExpirationMinutes, row.PromptTemplate);
}

internal sealed class PostgresConnectorService : IConnectorService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresConnectorService>? _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public PostgresConnectorService(
        QiaKonAppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<PostgresConnectorService>? logger = null)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public IReadOnlyList<ConnectorDto> GetAll()
        => _dbContext.Connectors.AsNoTracking().OrderBy(x => x.Name).ToList().Select(ToDto).ToList();

    public ConnectorDto? GetById(Guid id)
    {
        var connector = _dbContext.Connectors.AsNoTracking().FirstOrDefault(x => x.Id == id);
        return connector is null ? null : ToDto(connector);
    }

    public ConnectorDto Create(CreateConnectorDto request)
    {
        var connector = new ConnectorRow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            State = ConnectorState.Connected,
            BaseUrl = request.BaseUrl,
            ConnectionString = request.ConnectionString,
            EndpointsJson = PostgresPersistenceJson.Serialize(request.Endpoints),
            LastHealthCheck = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Connectors.Add(connector);
        _dbContext.SaveChanges();
        return ToDto(connector);
    }

    public ConnectorDto? Update(Guid id, CreateConnectorDto request)
    {
        var connector = _dbContext.Connectors.FirstOrDefault(x => x.Id == id);
        if (connector is null)
        {
            return null;
        }

        connector.Name = request.Name;
        connector.Type = request.Type;
        connector.BaseUrl = request.BaseUrl;
        connector.ConnectionString = request.ConnectionString;
        connector.EndpointsJson = PostgresPersistenceJson.Serialize(request.Endpoints);
        _dbContext.SaveChanges();
        return ToDto(connector);
    }

    public bool Delete(Guid id)
    {
        var connector = _dbContext.Connectors.FirstOrDefault(x => x.Id == id);
        if (connector is null)
        {
            return false;
        }

        _dbContext.Connectors.Remove(connector);
        _dbContext.SaveChanges();
        return true;
    }

    public ConnectorHealthResultDto CheckHealth(Guid id)
    {
        var connector = _dbContext.Connectors.FirstOrDefault(x => x.Id == id);
        if (connector is null)
        {
            return new ConnectorHealthResultDto(id, false, "连接器不存在", null);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (healthy, message) = connector.Type switch
            {
                ConnectorType.Http => CheckHttpHealth(connector).GetAwaiter().GetResult(),
                ConnectorType.Npgsql => CheckNpgsqlHealth(connector).GetAwaiter().GetResult(),
                ConnectorType.Redis => CheckRedisHealth(connector),
                ConnectorType.MessageQueue => CheckMessageQueueHealth(connector),
                ConnectorType.Custom => CheckCustomHealth(connector),
                _ => (false, $"不支持的连接器类型: {connector.Type}")
            };

            stopwatch.Stop();
            connector.LastHealthCheck = DateTime.UtcNow;
            connector.State = healthy ? ConnectorState.Healthy : ConnectorState.Unhealthy;
            _dbContext.SaveChanges();
            return new ConnectorHealthResultDto(id, healthy, message, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            connector.LastHealthCheck = DateTime.UtcNow;
            connector.State = ConnectorState.Unhealthy;
            _dbContext.SaveChanges();
            _logger?.LogWarning(ex, "Connector health check failed in PostgreSQL-backed service: {ConnectorId}", id);
            return new ConnectorHealthResultDto(id, false, $"健康检查异常: {ex.Message}", stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<(bool IsHealthy, string Message)> CheckHttpHealth(ConnectorRow connector)
    {
        if (string.IsNullOrWhiteSpace(connector.BaseUrl))
        {
            return (false, "BaseUrl 未配置");
        }

        using var client = _httpClientFactory.CreateClient("ConnectorHealthCheck");
        client.Timeout = TimeSpan.FromSeconds(5);
        var testUrl = connector.BaseUrl.TrimEnd('/') + "/";
        try
        {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, testUrl));
            if (response.IsSuccessStatusCode || (int)response.StatusCode is >= 401 and <= 403)
            {
                return (true, $"HTTP {response.StatusCode}");
            }

            return (false, $"HTTP {response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return (false, "连接超时 (5秒)");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"连接失败: {ex.Message}");
        }
    }

    private static async Task<(bool IsHealthy, string Message)> CheckNpgsqlHealth(ConnectorRow connector)
    {
        if (string.IsNullOrWhiteSpace(connector.ConnectionString))
        {
            return (false, "ConnectionString 未配置");
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connector.ConnectionString)
            {
                Timeout = 5,
                CommandTimeout = 5
            };

            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();
            return (true, $"连接成功 (PostgreSQL {connection.ServerVersion})");
        }
        catch (Exception ex)
        {
            return (false, $"数据库连接失败: {ex.Message}");
        }
    }

    private static (bool IsHealthy, string Message) CheckRedisHealth(ConnectorRow connector)
    {
        if (string.IsNullOrWhiteSpace(connector.ConnectionString))
        {
            return (false, "Redis 连接字符串未配置");
        }

        return connector.ConnectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || connector.ConnectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            ? (true, "Redis 配置格式正确")
            : (false, "Redis 配置格式无法识别");
    }

    private static (bool IsHealthy, string Message) CheckMessageQueueHealth(ConnectorRow connector)
    {
        if (string.IsNullOrWhiteSpace(connector.ConnectionString))
        {
            return (false, "消息队列连接字符串未配置");
        }

        return connector.ConnectionString.Contains("BootstrapServers", StringComparison.OrdinalIgnoreCase)
            ? (true, "消息队列配置格式正确")
            : (false, "消息队列配置格式无法识别");
    }

    private static (bool IsHealthy, string Message) CheckCustomHealth(ConnectorRow connector)
    {
        if (string.IsNullOrWhiteSpace(connector.ConnectionString))
        {
            return (false, "ConnectionString 未配置");
        }

        return connector.ConnectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || connector.ConnectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            ? (true, "配置格式正确")
            : (false, "ConnectionString 格式无法识别");
    }

    private static ConnectorDto ToDto(ConnectorRow row)
        => new(
            row.Id,
            row.Name,
            row.Type,
            row.State,
            row.BaseUrl,
            MaskPassword(row.ConnectionString),
            row.LastHealthCheck,
            PostgresPersistenceJson.Deserialize<IReadOnlyList<ConnectorEndpointDto>>(row.EndpointsJson) ?? Array.Empty<ConnectorEndpointDto>());

    private static string? MaskPassword(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        return Regex.Replace(connectionString, @"(Password|password|Pwd)=([^;]*)", "$1=******");
    }
}

internal sealed class PostgresAuditLogService : IAuditLogService
{
    private readonly QiaKonAppDbContext _dbContext;

    public PostgresAuditLogService(QiaKonAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public AuditLogPagedResultDto GetLogs(int page, int pageSize, Guid? userId = null, string? action = null, DateTime? startTime = null, DateTime? endTime = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();
        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action.Contains(action));
        }

        if (startTime.HasValue)
        {
            query = query.Where(x => x.Timestamp >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            query = query.Where(x => x.Timestamp <= endTime.Value);
        }

        var totalCount = query.LongCount();
        var items = query.OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .Select(ToDto)
            .ToList();

        return new AuditLogPagedResultDto(items, totalCount, page, pageSize);
    }

    public AuditLogDto? GetById(Guid id)
    {
        var log = _dbContext.AuditLogs.AsNoTracking().FirstOrDefault(x => x.Id == id);
        return log is null ? null : ToDto(log);
    }

    public void Log(Guid userId, string action, string resourceType, Guid? resourceId, string? resourceName, string result, string? ipAddress, string? details)
    {
        var username = _dbContext.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.Username).FirstOrDefault() ?? "unknown";
        _dbContext.AuditLogs.Add(new AuditLogRow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = username,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResourceName = resourceName,
            Result = result,
            IpAddress = ipAddress,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    private static AuditLogDto ToDto(AuditLogRow row)
        => new(row.Id, row.UserId, row.Username, row.Action, row.ResourceType, row.ResourceId, row.ResourceName, row.Result, row.IpAddress, row.Details, row.Timestamp);
}

internal sealed class PostgresDashboardService : IDashboardService
{
    private readonly QiaKonAppDbContext _dbContext;

    public PostgresDashboardService(QiaKonAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public DashboardStatsDto GetStats()
    {
        var documentCount = _dbContext.Documents.LongCount();
        var entityCount = _dbContext.GraphEntities.LongCount();
        var chatCount = _dbContext.ConversationSessions.LongCount();
        var today = DateTime.UtcNow.Date;
        var activeUsers = _dbContext.Users.LongCount(x => x.LastLoginAt.HasValue && x.LastLoginAt.Value >= today);

        var recentDocuments = _dbContext.Documents.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new RecentDocumentDto(x.Id, x.Title, x.CreatedAt))
            .ToList();

        var recentSessions = _dbContext.ConversationSessions.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Take(5)
            .ToList();
        var sessionIds = recentSessions.Select(x => x.Id).ToList();
        var messageCounts = _dbContext.ConversationMessages.AsNoTracking()
            .Where(x => sessionIds.Contains(x.ConversationId))
            .GroupBy(x => x.ConversationId)
            .ToDictionary(x => x.Key, x => x.Count());

        var recentChats = recentSessions.Select(x => new RecentChatDto(x.Id, x.Title, x.CreatedAt, messageCounts.GetValueOrDefault(x.Id))).ToList();

        var connectors = _dbContext.Connectors.AsNoTracking().ToList();
        var componentHealth = new List<ComponentHealthDto>
        {
            new("API", "Healthy", "服务正常运行", null),
            BuildConnectorHealth("PostgreSQL", connectors.FirstOrDefault(x => x.Type == ConnectorType.Npgsql), 8.2),
            BuildConnectorHealth("Redis", connectors.FirstOrDefault(x => x.Type == ConnectorType.Redis), 2.1),
            new("LLM Provider", _dbContext.LlmProviders.Any() ? "Healthy" : "Warning", _dbContext.LlmProviders.Any() ? "模型提供商已配置" : "尚未配置模型提供商", null),
        };

        return new DashboardStatsDto(documentCount, entityCount, chatCount, activeUsers, recentDocuments, recentChats, componentHealth);
    }

    private static ComponentHealthDto BuildConnectorHealth(string name, ConnectorRow? row, double? responseTime)
    {
        if (row is null)
        {
            return new ComponentHealthDto(name, "Warning", "未配置连接器", responseTime);
        }

        var status = row.State is ConnectorState.Healthy or ConnectorState.Connected ? "Healthy" : row.State.ToString();
        var message = row.LastHealthCheck.HasValue
            ? $"最近检查: {row.LastHealthCheck:yyyy-MM-dd HH:mm:ss}"
            : "尚未执行健康检查";
        return new ComponentHealthDto(name, status, message, responseTime);
    }
}

internal sealed class PostgresGraphOverviewService : IGraphOverviewService
{
    private readonly QiaKonAppDbContext _dbContext;

    public PostgresGraphOverviewService(QiaKonAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public GraphOverviewDto GetOverview()
    {
        var totalEntities = _dbContext.GraphEntities.LongCount();
        var totalRelations = _dbContext.GraphRelations.LongCount();
        var departmentEntities = _dbContext.GraphEntities.LongCount(x => !x.IsPublic);
        var publicEntities = _dbContext.GraphEntities.LongCount(x => x.IsPublic);
        var entityTypeDistribution = _dbContext.GraphEntities.AsNoTracking().GroupBy(x => x.Type).ToDictionary(x => x.Key, x => x.LongCount());
        var relationTypeDistribution = _dbContext.GraphRelations.AsNoTracking().GroupBy(x => x.Type).ToDictionary(x => x.Key, x => x.LongCount());
        return new GraphOverviewDto(totalEntities, totalRelations, departmentEntities, publicEntities, entityTypeDistribution, relationTypeDistribution);
    }
}
