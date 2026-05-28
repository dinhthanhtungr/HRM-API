namespace HRM.Application.Features.Auth.Contracts;

public sealed class LoginResultDto
{
    public string AccessToken { get; init; } = string.Empty;

    public DateTime ExpiresAtUtc { get; init; }

    public Guid UserId { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public Guid? EmployeeId { get; init; }
    public Guid? CompanyId { get; init; }

    public string RefreshToken { get; init; } = string.Empty;
    public DateTime RefreshTokenExpireAtUtc { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
