using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("login")]
    public ApiResponse<object> Login([FromBody] LoginRequest request)
    {
        // 临时模拟验证
        if (request.Username != "admin" || request.Password != "admin123")
        {
            return ApiResponse<object>.Fail("Invalid username or password", 401);
        }

        var token = GenerateJwtToken(request.Username);
        return ApiResponse<object>.Ok(new { token, expiresIn = 3600 }, "Login successful");
    }

    [HttpPost("refresh")]
    public ApiResponse<object> Refresh([FromBody] RefreshTokenRequest request)
    {
        // 临时模拟，实际应验证 refresh token
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return ApiResponse<object>.Fail("Refresh token is required", 400);
        }

        var token = GenerateJwtToken("user");
        return ApiResponse<object>.Ok(new { token, expiresIn = 3600 }, "Token refreshed");
    }

    private string GenerateJwtToken(string username)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"]!;
        var issuer = jwtSection["Issuer"]!;
        var audience = jwtSection["Audience"]!;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string Username, string Password);
public record RefreshTokenRequest(string RefreshToken);
