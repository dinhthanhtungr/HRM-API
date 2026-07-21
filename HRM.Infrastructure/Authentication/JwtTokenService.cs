using HRM.Application.Abstractions.Authentication;
using HRM.Application.Features.Auth.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace HRM.Infrastructure.Authentication;

public sealed class JwtTokenService(IConfiguration configuration) : ITokenService
{
    private const int DefaultExpirationMinutes = 30;

    public AccessTokenDto CreateAccessToken(AuthenticatedUserDto user)
    {
        var expirationMinutes = int.TryParse(configuration["Jwt:EXPIRATION_MINUTES"], out var configuredMinutes)
            && configuredMinutes > 0
            ? configuredMinutes
            : DefaultExpirationMinutes;
        var expiresAt = DateTime.Now.AddMinutes(expirationMinutes);

        var roles = user.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("employeeId", user.EmployeeId?.ToString() ?? string.Empty),
            new("companyId", user.CompanyId?.ToString() ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim("roles", role)));

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.")));

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AccessTokenDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt
        };
    }
    public string CreateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

}
