using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Admin, President, and Developer auto-approve only when create has attachments.
/// Xác định quyền duyệt SaleOrder và trường hợp Sale thường được tự động duyệt khi tạo kèm PO.
/// AC/HN luôn đi qua luồng duyệt thủ công; nhóm có quyền duyệt cũng không dùng nhánh auto approve.
/// </summary>
internal static class SaleOrderApprovalRules
{
    public static bool CanApprove(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SaleOrderApprovers);

    public static bool CanAutoApproveOnCreate(ICurrentUser currentUser)
    {
        if (currentUser.IsInAnyRole(new[]
            {
                ApplicationRoles.Admin,
                ApplicationRoles.President,
                ApplicationRoles.Developer
            }))
        {
            return true;
        }

        return currentUser.IsInRole(ApplicationRoles.Sales.SaleUser) &&
               !currentUser.IsInRole(ApplicationRoles.Accounting.ACUser) &&
               !currentUser.IsInRole(ApplicationRoles.Accounting.HNUser) &&
               !CanApprove(currentUser);
    }
}
