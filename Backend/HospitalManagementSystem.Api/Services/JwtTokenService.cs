using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HospitalManagementSystem.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace HospitalManagementSystem.Api.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public JwtTokenResult Create(User user, int? doctorId)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret is not configured.");
        if (secret.Length < 32)
            throw new InvalidOperationException("JWT secret must contain at least 32 characters.");

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        if (doctorId.HasValue) claims.Add(new Claim("doctorId", doctorId.Value.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "MediCore.Api",
            audience: _configuration["Jwt:Audience"] ?? "MediCore.Client",
            claims: claims, expires: expiresAt, signingCredentials: credentials);
        return new JwtTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
