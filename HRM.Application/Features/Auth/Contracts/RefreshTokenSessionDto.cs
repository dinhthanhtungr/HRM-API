namespace HRM.Application.Features.Auth.Contracts;

public sealed record RefreshTokenSessionDto(
    string RefreshToken,
    DateTime ExpiresAtUtc);
