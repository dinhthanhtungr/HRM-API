namespace HRM.Application.Features.Auth.Contracts;

public sealed class AccessTokenDto
{
    public string Token { get; init; } = string.Empty;

    public DateTime ExpiresAtUtc { get; init; }
}
