namespace HRM.Application.Features.Auth.Contracts;

public sealed class AuthenticatedUserDto
{
    public Guid UserId { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public Guid? EmployeeId { get; init; }
    public Guid? CompanyId { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
