namespace HRM.Application.Abstractions.Identity;

public sealed record EmployeeIdentityAccount(
    Guid UserId,
    string? UserName,
    string? Email,
    bool IsActive,
    IReadOnlyList<string> ActiveRoles);

public sealed record EmployeeIdentityRole(
    Guid RoleId,
    string Name,
    int ActiveAssignmentCount);

public sealed record IdentityAdministrationResult(
    bool Success,
    string? Error)
{
    public static IdentityAdministrationResult Ok() => new(true, null);
    public static IdentityAdministrationResult Fail(string error) => new(false, error);
}

public sealed record IdentityAdministrationResult<T>(
    bool Success,
    T? Data,
    string? Error)
{
    public static IdentityAdministrationResult<T> Ok(T data) => new(true, data, null);
    public static IdentityAdministrationResult<T> Fail(string error) => new(false, default, error);
}

public interface IEmployeeIdentityAdministrationService
{
    Task<EmployeeIdentityAccount?> GetAccountAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeIdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<IdentityAdministrationResult<EmployeeIdentityAccount>> CreateAccountAsync(
        Guid employeeId,
        string userName,
        string? email,
        string password,
        CancellationToken cancellationToken = default);

    Task<IdentityAdministrationResult<EmployeeIdentityAccount>> SetAccountActiveAsync(
        Guid employeeId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<IdentityAdministrationResult> AssignRoleAsync(
        Guid employeeId,
        string roleName,
        CancellationToken cancellationToken = default);

    Task<IdentityAdministrationResult> RevokeRoleAsync(
        Guid employeeId,
        string roleName,
        CancellationToken cancellationToken = default);

    Task<IdentityAdministrationResult<EmployeeIdentityRole>> CreateRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default);
}
