namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Resolve nguoi nhan theo sale group thuc te. "Leader" trong CRM duoc hieu la admin cua group sale,
/// khong phai moi user co role Leader tren toan cong ty.
/// </summary>
public interface ISaleGroupRecipientResolver
{
    Task<IReadOnlyCollection<Guid>> ResolveSaleGroupLeaderIdsAsync(
        Guid companyId,
        Guid saleEmployeeId,
        Guid? excludeEmployeeId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> ResolveActiveEmployeeIdsByRolesAsync(
        Guid companyId,
        IReadOnlyCollection<string> normalizedRoleNames,
        Guid? excludeEmployeeId = null,
        CancellationToken cancellationToken = default);
}
