using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ExportManufacturingFormulaExcel;

internal sealed class ExportManufacturingFormulaExcelQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility,
    IManufacturingFormulaExcelRenderer renderer
) : IRequestHandler<ExportManufacturingFormulaExcelQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(ExportManufacturingFormulaExcelQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Không có quyền xem vật tư công thức.");
        }

        if (request.IncludePrices && (!user.IsInAnyRole(ApplicationRoleSets.PLM.ManufacturingPricedExcelExporters)
            || !visibility.CanViewFormulaPrices()))
            return Fail("Bạn không có quyền xuất Excel có giá công thức.");

        if (request.OrderId == Guid.Empty)
        {
            return Fail("Lệnh sản xuất không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var order = await db.MfgProductionOrders.AsNoTracking().Where(x => x.MfgProductionOrderId == request.OrderId && x.IsActive && x.CompanyId == company)
            .Select(x => new { x.ExternalId, x.ProductExternalIdSnapshot, x.ProductNameSnapshot })
            .FirstOrDefaultAsync(ct);
        if (order is null)
        {
            return Fail("Không tìm thấy lệnh sản xuất active trong công ty.");
        }

        var selected = await (
            from v in db.ProductionSelectVersions.AsNoTracking()join f in db.ManufacturingFormulas.AsNoTracking()on v.ManufacturingFormulaId equals f.ManufacturingFormulaId
            where v.CompanyId == company && f.CompanyId == company && v.MfgProductionOrderId == request.OrderId
            && v.ValidTo == null
            && f.IsActive
            orderby v.ValidFrom descending, v.ProductionSelectVersionId descending
            select new
            {
                f.ManufacturingFormulaId,
                f.ExternalId
            }

        ).FirstOrDefaultAsync(ct);
        if (selected is null)
        {
            return Fail("Lệnh chưa chọn VA active hiện hành.");
        }

        var rows = await ManufacturingFormulaExportRowReader.ReadAsync(
            db,
            company,
            selected.ManufacturingFormulaId,
            selected.ExternalId,
            order.ExternalId,
            order.ProductExternalIdSnapshot,
            order.ProductNameSnapshot,
            0,
            ct,
            request.IncludePrices
        );
        if (request.IncludePrices && rows.Count == 0) return Fail("VA chưa có dòng NVL để xuất Excel.");
        try
        {
            var bytes = renderer.Render(rows);
            return bytes.Length == 0 ? Fail("Không thể tạo Excel công thức.") : OperationResult<byte[]>.Ok(bytes);
        }
        catch (Exception ex)when (ex is not OperationCanceledException)
        {
            return Fail("Không thể tạo Excel công thức.");
        }
    }

    private static OperationResult<byte[]> Fail(string message) => OperationResult<byte[]>.Fail(message);
}
