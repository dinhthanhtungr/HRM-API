using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Nhóm duyệt đơn và Sale thường được tự động duyệt chỉ khi tạo kèm PO.
/// HN không tự duyệt chỉ bằng role SaleUser.
/// </summary>
internal static class SaleOrderApprovalRules
{
    public static bool CanApprove(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SaleOrderApprovers);

    public static bool CanAutoApproveOnCreate(ICurrentUser currentUser)
    {
        if (CanApprove(currentUser))
        {
            return true;
        }

        return currentUser.IsInRole(ApplicationRoles.Sales.SaleUser) &&
               !currentUser.IsInRole(ApplicationRoles.Accounting.HNUser) &&
               !CanApprove(currentUser);
    }
}
