using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>Sale phụ trách hoặc nhóm được chỉ định được xác nhận, luôn trong cùng company.</summary>
internal static class StandardSampleConfirmationRules
{
    public static bool CanConfirm(ICurrentUser user, Guid companyId, Guid managerId)
        => user.IsAuthenticated && user.EmployeeId is { } actor && actor != Guid.Empty
           && companyId != Guid.Empty && user.CompanyId == companyId
           && (actor == managerId || user.IsInAnyRole(ApplicationRoleSets.PLM.StandardSampleConfirmers));

    public static bool CanConfirmOtherOwners(ICurrentUser user)
        => CanConfirm(user, user.CompanyId ?? Guid.Empty, Guid.Empty);
}
