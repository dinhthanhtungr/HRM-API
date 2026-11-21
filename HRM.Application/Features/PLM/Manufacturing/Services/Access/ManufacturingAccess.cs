using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Access;

internal static class ManufacturingAccess
{
    public static async Task<string?> FormulaEditLockAsync(IManufacturingDbContext db, MfgProductionOrder order, CancellationToken ct)
    {
        // Reservations alone are not issue requests. Retain the lock after an actual issue,
        // even if the request was subsequently cancelled or the MFG was completed.
        var links = db.OperationMaterialRequestLinks.AsNoTracking().Where(x => x.CompanyId == order.CompanyId
            && x.MfgProductionOrderId == order.MfgProductionOrderId);
        var requestCode = await IssueRequestCodes(db, order).FirstOrDefaultAsync(ct);
        if (requestCode is not null)
            return $"Đã có yêu cầu xuất kho {requestCode}; không thể thay đổi NVL, Std, lot, loại hàng hoặc khối lượng sản xuất.";
        var issued = await db.TempStockIssueLedgers.AsNoTracking().AnyAsync(x => x.CompanyId == order.CompanyId && x.QtyKg > 0
            && (order.ExternalId != "" && x.VaCode == order.ExternalId || links.Any(l => l.RequestCode != "" && l.RequestCode == x.RequestCode
                || l.VaExternalId != null && l.VaExternalId != "" && l.VaExternalId == x.VaCode)
                || db.ProductionSelectVersions.Any(v => v.CompanyId == order.CompanyId && v.MfgProductionOrderId == order.MfgProductionOrderId
                    && v.ManufacturingFormula != null && v.ManufacturingFormula.CompanyId == order.CompanyId
                    && v.ManufacturingFormula.ExternalId == x.VaCode)), ct);
        return issued ? "Lệnh đã có dữ liệu xuất kho thực tế; không thể thay đổi công thức hoặc khối lượng sản xuất." : null;
    }

    internal static IQueryable<string> IssueRequestCodes(IManufacturingDbContext db, MfgProductionOrder order)
        => from link in db.OperationMaterialRequestLinks.AsNoTracking()
            from request in db.WarehouseRequests.AsNoTracking()
            where link.CompanyId == order.CompanyId && link.MfgProductionOrderId == order.MfgProductionOrderId
                && request.CompanyId == order.CompanyId
                && (link.RequestId == request.RequestId || link.RequestId == null && link.RequestCode != "" && link.RequestCode == request.RequestCode)
                && (request.ReqType == WareHouseRequestType.ExportRawMaterial || request.ReqType == WareHouseRequestType.ExportDefectiveRawMaterial
                    || request.ReqType == WareHouseRequestType.ExportFinishedGood || request.ReqType == WareHouseRequestType.ExportDefectiveFinishedGood
                    || request.ReqType == WareHouseRequestType.ExportOther || request.ReqType == WareHouseRequestType.ExportMaterial)
                && (request.ReqStatus != WarehouseRequestStatus.Cancelled || request.WarehouseVouchers.Any(v => v.CompanyId == order.CompanyId
                    && v.Details.Any(d => d.IsApplied && !d.IsIncrease && d.QtyKg > 0)))
            orderby request.RequestId
            select request.RequestCode;

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
