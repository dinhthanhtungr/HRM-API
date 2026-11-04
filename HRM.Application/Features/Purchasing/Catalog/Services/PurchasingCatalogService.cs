using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService(
    IPLMWriteDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    IDateTimeProvider clock, IExternalIdService codes) : IPurchasingCatalogService
{
    private Guid RequireReadContext()
    {
        if (!user.IsAuthenticated ||
            !(permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders) || permissions.HasPermission(ApplicationPermissions.Purchasing.ManageCatalog)) ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty)
            throw new UnauthorizedAccessException("Không có quyền quản lý danh mục mua hàng.");
        return companyId;
    }

    private async Task<(Guid CompanyId, Guid EmployeeId)> RequireWriteContextAsync(CancellationToken ct)
    {
        var companyId = RequireReadContext();
        if (!permissions.HasPermission(ApplicationPermissions.Purchasing.ManageCatalog))
            throw new UnauthorizedAccessException("Không có quyền sửa danh mục mua hàng.");
        if (user.EmployeeId is not { } employeeId || employeeId == Guid.Empty ||
            !await db.Employees.AnyAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId && x.IsActive == true, ct))
            throw new UnauthorizedAccessException("Nhân viên hiện tại không thuộc công ty hoặc đã ngừng hoạt động.");
        return (companyId, employeeId);
    }

    private static OperationResult<CatalogSavedDto> Fail(string error) => OperationResult<CatalogSavedDto>.Fail(error);
    private static OperationResult<CatalogSavedDto> Saved(Guid id, string? code, DateTime now) =>
        OperationResult<CatalogSavedDto>.Ok(new CatalogSavedDto(id, code, now));
    private const string ConflictMessage = "Dữ liệu đã thay đổi. Hãy tải lại trước khi sửa.";

    // PostgreSQL timestamp lưu microsecond; token trả FE phải trùng giá trị được persist.
    private DateTime NextUpdate(DateTime? previous = null)
    {
        var now = clock.Now;
        var ticks = now.Ticks - now.Ticks % 10;
        if (previous.HasValue && ticks <= previous.Value.Ticks)
            ticks = previous.Value.Ticks - previous.Value.Ticks % 10 + 10;
        return new DateTime(ticks, now.Kind);
    }
}
