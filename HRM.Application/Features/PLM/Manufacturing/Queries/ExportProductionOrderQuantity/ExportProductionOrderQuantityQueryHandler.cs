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

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ExportProductionOrderQuantity;

internal sealed class ExportProductionOrderQuantityQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility,
    IProductionOrderQuantityExcelRenderer renderer
) : IRequestHandler<ExportProductionOrderQuantityQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(ExportProductionOrderQuantityQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Không có quyền xem vật tư công thức.");
        }

        var code = request.FormulaExternalId?.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 255)
        {
            return Fail("Mã VA không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var selections = await (
            from formula in db.ManufacturingFormulas.AsNoTracking()join version in db.ProductionSelectVersions.AsNoTracking()on formula.ManufacturingFormulaId equals version.ManufacturingFormulaId
            join order in db.MfgProductionOrders.AsNoTracking()on version.MfgProductionOrderId equals order.MfgProductionOrderId
            where formula.CompanyId == company && version.CompanyId == company && order.CompanyId == company
            && formula.ExternalId == code
            && formula.IsActive
            && order.IsActive
            && version.ValidTo == null
            orderby version.ValidFrom descending
            select new
            {
                formula.ManufacturingFormulaId,
                FormulaCode = formula.ExternalId,
                order.ExternalId,
                order.ProductExternalIdSnapshot,
                order.ProductNameSnapshot,
                order.TotalQuantity
            }

        ).Take(2)
            .ToListAsync(ct);
        if (selections.Count == 0)
        {
            return Fail("Không tìm thấy VA đang được lệnh sản xuất sử dụng trong công ty.");
        }

        if (selections.Count > 1)
        {
            return Fail("VA đang được nhiều lệnh sản xuất sử dụng; không xác định được khối lượng duy nhất.");
        }

        var selection = selections[0];
        if (selection.TotalQuantity is null or <= 0)
        {
            return Fail("Lệnh sản xuất chưa có khối lượng hợp lệ.");
        }

        var rows = await ManufacturingFormulaExportRowReader.ReadAsync(
            db,
            company,
            selection.ManufacturingFormulaId,
            selection.FormulaCode,
            selection.ExternalId,
            selection.ProductExternalIdSnapshot,
            selection.ProductNameSnapshot,
            selection.TotalQuantity.Value,
            ct
        );
        try
        {
            var bytes = renderer.Render(rows);
            return bytes.Length == 0 ? Fail("Không thể tạo Excel khối lượng.") : OperationResult<byte[]>.Ok(bytes);
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            return Fail("Không thể tạo Excel khối lượng.");
        }
    }

    private static OperationResult<byte[]> Fail(string message) => OperationResult<byte[]>.Fail(message);
}
