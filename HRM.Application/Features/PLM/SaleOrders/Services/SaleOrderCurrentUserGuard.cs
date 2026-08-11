using HRM.Application.Abstractions.Security;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

/// <summary>
/// Bảo đảm các handler SaleOrder luôn có employee và company hợp lệ từ current user.
/// </summary>
internal static class SaleOrderCurrentUserGuard
{
    /// <summary>
    /// Trả về employee hiện tại hoặc fail rõ khi tài khoản chưa gắn với nhân viên.
    /// </summary>
    public static Guid RequireEmployeeId(ICurrentUser currentUser)
    {
        return currentUser.EmployeeId is { } employeeId && employeeId != Guid.Empty
            ? employeeId
            : throw new InvalidOperationException("Current user has no employee context.");
    }

    /// <summary>
    /// Trả về company hiện tại để mọi thao tác SaleOrder giữ đúng tenant scope.
    /// </summary>
    public static Guid RequireCompanyId(ICurrentUser currentUser)
    {
        return currentUser.CompanyId is { } companyId && companyId != Guid.Empty
            ? companyId
            : throw new InvalidOperationException("Current user has no company context.");
    }
}
