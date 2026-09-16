namespace HRM.Application.Abstractions.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    Guid? EmployeeId { get; }

    Guid? CompanyId { get; }

    string? UserName { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> Permissions => Array.Empty<string>();

    bool HasExplicitPermissionSet => false;

    bool IsInRole(string role);
}
