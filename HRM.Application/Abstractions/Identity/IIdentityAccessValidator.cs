namespace HRM.Application.Abstractions.Identity;

/// <summary>
/// Revalidates mutable account and employee access state for an otherwise valid JWT.
/// </summary>
public interface IIdentityAccessValidator
{
    Task<bool> IsAccessAllowedAsync(
        Guid userId,
        Guid? employeeId,
        Guid? companyId,
        CancellationToken cancellationToken = default);
}
