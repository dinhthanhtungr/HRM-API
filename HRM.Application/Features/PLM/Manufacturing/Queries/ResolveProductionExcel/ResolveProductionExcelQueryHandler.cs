using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ResolveProductionExcel;

internal sealed class ResolveProductionExcelQueryHandler(IManufacturingDbContext db, IPLMReadDbContext readDb, ICurrentUser user,
    ICurrentUserPermissionService permissions, IPLMFieldVisibilityService visibility, ISender sender)
    : IRequestHandler<ResolveProductionExcelQuery, OperationResult<IReadOnlyList<ProductionExcelResolution>>>
{
    public async Task<OperationResult<IReadOnlyList<ProductionExcelResolution>>> Handle(ResolveProductionExcelQuery request, CancellationToken ct)
    {
        var access = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageProductionOrders, true);
        if (access is not null || !user.IsInAnyRole(ApplicationRoleSets.PLM.ProductionExcelImporters)
            || !permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders)
            || !visibility.CanViewFormulaMaterials() || !visibility.CanViewManufacturingDefectiveLots())
            return Fail("Bạn không có quyền nhập công thức từ Excel.");
        if (request.Rows is not { Length: > 0 and <= 200 }
            || request.Rows.Any(x => x is null || new[] { x.ColourCode, x.Name, x.LotNo, x.Quality }
                .Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 500)))
            return Fail("Cần 1–200 dòng đủ 4 cột: mã màu, tên, lot và loại hàng.");
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.Id
            && x.CompanyId == user.CompanyId && x.IsActive, ct);
        if (order is null) return Fail("Không tìm thấy lệnh trong công ty.");
        var stateError = await ManufacturingAccess.FormulaEditLockAsync(db, order, ct);
        if (stateError is not null) return Fail(stateError);
        if (!ManufacturingEditorPolicy.Actions(order.Status, true, true, true).Contains("Save"))
            return Fail("Trạng thái lệnh chưa cho phép sửa công thức.");
        var linked = await db.MfgOrderPOs.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == request.Id && x.IsActive
            && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive && x.Detail.MerchandiseOrder.CompanyId == user.CompanyId, ct);
        if (!linked) return Fail("Lệnh không còn liên kết đơn bán hàng hợp lệ.");
        var codes = request.Rows.Select(x => Normalize(x.ColourCode)).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && x.IsActive
            && x.ColourCode != null && codes.Contains(x.ColourCode.Trim().ToUpper()))
            .Select(x => new { x.ProductId, x.CategoryId, x.Unit, x.ColourCode }).ToListAsync(ct);
        var displays = products.Select(x => new FormulaItemDisplaySource(x.ProductId, ItemType.Product, null, null)).ToArray();
        var current = await FormulaItemDisplayResolver.LoadCurrentDataAsync(readDb, user.CompanyId!.Value, displays, ct);
        // Tái sử dụng scope/visibility của kho; không ghi hay giữ chỗ khi đối chiếu.
        var stock = await sender.Send(new GetStockAvailableQuery { ExactCodes = products.Select(x => x.ColourCode!).Distinct().ToArray(),
            StockTypes = [StockType.FinishedGood, StockType.DefectiveFinishedGood], ExportAll = true }, ct);
        if (!stock.Success || stock.Data is null) return Fail("Không đọc được tồn kho. Vui lòng thử lại.");
        var result = new List<ProductionExcelResolution>();
        var keys = new HashSet<string>();
        for (var index = 0; index < request.Rows.Length; index++)
        {
            var row = request.Rows[index];
            string? error = null;
            ProductionExcelItem? item = null;
            var matches = products.Where(x => Normalize(x.ColourCode!) == Normalize(row.ColourCode)).ToArray();
            var quality = Normalize(row.Quality);
            var type = quality == "TP LỖI" ? StockType.DefectiveFinishedGood : StockType.FinishedGood;
            if (quality is not ("TP LỖI" or "TP ĐẠT" or "TP")) error = "InvalidQuality";
            else if (matches.Length != 1) error = matches.Length == 0 ? "ProductNotFound" : "AmbiguousProduct";
            else
            {
                var product = matches[0];
                var kg = stock.Data.Items.Where(x => Normalize(x.Code) == Normalize(row.ColourCode) && x.StockType == type)
                    .SelectMany(x => x.StockDetailAvailables)
                    .Where(x => x.CanUse && Normalize(x.LotNo ?? "N/A") == Normalize(row.LotNo)).Sum(x => x.OnHandKg);
                var key = $"{product.ProductId}:{type}:{Normalize(row.LotNo)}";
                if (kg <= 0) error = "NoStock";
                else if (!keys.Add(key)) error = "DuplicateItemLot";
                else if (product.CategoryId == Guid.Empty || !string.Equals(product.Unit, "kg", StringComparison.OrdinalIgnoreCase)) error = "InvalidProduct";
                else
                {
                    var display = FormulaItemDisplayResolver.Resolve(new FormulaItemDisplaySource(product.ProductId, ItemType.Product, null, null), current);
                    item = new(product.ProductId, type == StockType.DefectiveFinishedGood ? 3 : 1, product.CategoryId,
                        display.ExternalId ?? product.ColourCode!, display.Name ?? row.Name, product.ColourCode!, "kg",
                        row.LotNo.Trim(), (int)type, kg, 0);
                }
            }
            result.Add(new(index + 1, item, error));
        }
        if (result.All(x => x.Item is not null))
        {
            var totalKg = result.Sum(x => x.Item!.StockKg);
            decimal allocated = 0;
            for (var index = 0; index < result.Count; index++)
            {
                var item = result[index].Item!;
                var percent = index == result.Count - 1 ? 100m - allocated
                    : decimal.Truncate(item.StockKg / totalKg * 100 * 10000000000m) / 10000000000m;
                allocated += percent;
                result[index] = result[index] with { Item = item with { Percent = percent } };
            }
        }
        return OperationResult<IReadOnlyList<ProductionExcelResolution>>.Ok(result);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static OperationResult<IReadOnlyList<ProductionExcelResolution>> Fail(string message)
        => OperationResult<IReadOnlyList<ProductionExcelResolution>>.Fail(message);
}
