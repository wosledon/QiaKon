namespace QiaKon.Contracts.DTOs;

/// <summary>
/// 登录请求
/// </summary>
public sealed record LoginRequestDto(
    string Username,
    string Password);

/// <summary>
/// 登录响应
/// </summary>
public sealed record LoginResponseDto(
    string Token,
    int ExpiresIn,
    UserDto User);

/// <summary>
/// 用户信息DTO
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    Guid DepartmentId,
    string DepartmentName,
    UserRole Role,
    bool IsActive);

/// <summary>
/// 刷新令牌请求
/// </summary>
public sealed record RefreshTokenRequestDto(string RefreshToken);
