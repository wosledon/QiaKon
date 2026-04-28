using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

/// <summary>
/// 内存态认证服务实现（带种子数据）
/// </summary>
public sealed class MemoryAuthService : IAuthService
{
    private readonly Dictionary<Guid, (UserDto User, string PasswordHash)> _users = new();
    private readonly Dictionary<string, (Guid UserId, DateTime ExpiresAt)> _tokens = new();
    private readonly Dictionary<Guid, DepartmentInfo> _departments = new();
    private readonly ILogger<MemoryAuthService>? _logger;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public MemoryAuthService(ILogger<MemoryAuthService>? logger = null)
    {
        _logger = logger;
        _jwtSecret = "QiaKon-Dev-Secret-Key-For-Development-Only-Min-32-Chars!";
        _jwtIssuer = "QiaKon";
        _jwtAudience = "QiaKon.Api";

        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        // 种子部门
        var deptEngineering = new DepartmentInfo { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "研发部" };
        var deptSales = new DepartmentInfo { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "销售部" };
        var deptHR = new DepartmentInfo { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "人力资源部" };
        var deptAdmin = new DepartmentInfo { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "行政部" };

        _departments[deptEngineering.Id] = deptEngineering;
        _departments[deptSales.Id] = deptSales;
        _departments[deptHR.Id] = deptHR;
        _departments[deptAdmin.Id] = deptAdmin;

        // 种子用户 (密码都是 "password123")
        var users = new[]
        {
            (Id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Username: "admin", Email: "admin@qiakon.com", Password: "password123", DeptId: deptAdmin.Id, Role: UserRole.Admin),
            (Id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Username: "kb_admin", Email: "kb_admin@qiakon.com", Password: "password123", DeptId: deptEngineering.Id, Role: UserRole.KnowledgeAdmin),
            (Id: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Username: "dept_manager", Email: "dept_mgr@qiakon.com", Password: "password123", DeptId: deptEngineering.Id, Role: UserRole.DepartmentManager),
            (Id: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Username: "engineer", Email: "engineer@qiakon.com", Password: "password123", DeptId: deptEngineering.Id, Role: UserRole.DepartmentMember),
            (Id: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Username: "guest", Email: "guest@qiakon.com", Password: "password123", DeptId: deptSales.Id, Role: UserRole.Guest),
        };

        foreach (var u in users)
        {
            _users[u.Id] = (new UserDto(
                Id: u.Id,
                Username: u.Username,
                Email: u.Email,
                DepartmentId: u.DeptId,
                DepartmentName: _departments[u.DeptId].Name,
                Role: u.Role,
                IsActive: true
            ), u.Password);
        }

        _logger?.LogInformation("MemoryAuthService initialized with {UserCount} users and {DeptCount} departments", _users.Count, _departments.Count);
    }

    public LoginResponseDto? Login(LoginRequestDto request)
    {
        var user = _users.Values.FirstOrDefault(u => u.User.Username == request.Username);
        if (user.User == null || user.PasswordHash != request.Password)
        {
            _logger?.LogWarning("Login failed for username: {Username}", request.Username);
            return null;
        }

        if (!user.User.IsActive)
        {
            _logger?.LogWarning("Login failed - user inactive: {Username}", request.Username);
            return null;
        }

        var token = GenerateJwtToken(user.User);
        var expiresIn = 3600;

        _tokens[token] = (user.User.Id, DateTime.UtcNow.AddSeconds(expiresIn));

        _logger?.LogInformation("User logged in: {Username}", request.Username);

        return new LoginResponseDto(token, expiresIn, user.User);
    }

    public (bool IsValid, Guid UserId, UserRole Role) ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return (false, Guid.Empty, UserRole.Guest);

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
        return _users.TryGetValue(userId, out var user) ? user.User : null;
    }

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
            signingCredentials: credentials
        );

        return handler.WriteToken(token);
    }

    private class DepartmentInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
