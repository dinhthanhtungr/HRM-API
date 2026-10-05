using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.ProductionOrders.Services;

internal static class ProductionOrderCreationAccess
{
    public static string? Validate(ICurrentUser user, ICurrentUserPermissionService permissions)
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.PLM.CreateProductionOrders))
            return "Bạn không có quyền tạo lệnh sản xuất.";
        if (user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return "Tài khoản chưa có công ty hoặc nhân viên hợp lệ.";
        return null;
    }
}
