using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Access;

internal static class ManufacturingAccess
{
    public static string? ValidateFormulaEditState(string status)
        => status is "Scheduling" or "Scheduled" or "Unassign" or "Getback"
            or "QCinprogress" or "QCInProgress" or "QCPassed" or "QCFail" or "BTPNew"
            or "Weighting" or "Weighted" or "Mixing" or "Mixed" or "Started" or "Canceled"
            or "Running" or "Change" or "Unassignfrommd" or "UnassignFromMD" or "Reported"
            ? "Lệnh đã yêu cầu xuất kho; nguyên vật liệu và Std bị khóa đến khi lệnh về Done hoặc Finished."
            : null;

    public static bool IsInformOperator(ICurrentUser user)
        => user.IsInAnyRole(ApplicationRoleSets.PLM.ProductionInformOperators);

    public static bool CanManageInform(ICurrentUser user, ICurrentUserPermissionService permissions)
        => IsInformOperator(user) && permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders);

    public static string? ValidateInformState(ICurrentUser user, string status)
        => status == "New" && !IsInformOperator(user)
            ? "Lệnh Mới chỉ PLPUUser, President và Developer được thao tác; LabUser chỉ được xem." : null;

    // Công thức/ghi chú là quyền riêng; không mở quyền quản lý header cho Lab.
    public static string? ValidateEditor(ICurrentUser user, ICurrentUserPermissionService permissions)
    {
        if (!IsInformOperator(user) && !user.IsInRole(ApplicationRoles.Lab.LabUser))
            return "Bạn không có quyền sửa Inform lệnh sản xuất.";
        var capability = permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
            ? ApplicationPermissions.PLM.ManageProductionOrders
            : ApplicationPermissions.PLM.ManageManufacturingFormulas;
        if (capability == ApplicationPermissions.PLM.ManageManufacturingFormulas
            && !permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders))
            return "Bạn không có quyền xem lệnh sản xuất.";
        return Validate(user, permissions, capability, true);
    }

    // PLPU note thuộc nội dung quản lý lệnh; quyền sửa công thức của Lab không cấp quyền nhập tay.
    // Chỉ kiểm tra đầu vào: ghi chú thay đổi NVL do server sinh vẫn hoạt động bình thường.
    public static string? ValidatePlpuNoteChange(ICurrentUserPermissionService permissions,
        string? stored, string? requested)
        => requested is null || string.Equals(stored, requested, StringComparison.Ordinal)
            || permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
            ? null : "Bạn không có quyền sửa Ghi chú PLPU.";

    public static string? ValidateRequirementChange(ICurrentUserPermissionService permissions,
        string? stored, string? requested)
        => requested is null || string.Equals(stored, requested, StringComparison.Ordinal)
            || permissions.HasPermission(ApplicationPermissions.PLM.EditProductionOrderRequirement)
            ? null : "Bạn không có quyền sửa nội dung Yêu cầu.";

    public static string? Validate(
        ICurrentUser user,
        ICurrentUserPermissionService permissions,
        string capability,
        bool requiresEmployee = false
    )
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(capability))
        {
            return "Bạn không có quyền thực hiện thao tác này.";
        }

        if (user.CompanyId is not { } company || company == Guid.Empty)
        {
            return "Tài khoản chưa có công ty hợp lệ.";
        }

        if (requiresEmployee && (user.EmployeeId is not { } employee || employee == Guid.Empty))
        {
            return "Tài khoản chưa có nhân viên hợp lệ.";
        }

        return null;
    }
}
