using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Shared.Authorization;
using Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;

internal sealed class ExportManufacturingVaQueryHandler(
    IPLMReadDbContext db, IWarehouseReadDbContext warehouse, ICurrentUser user,
    ICurrentUserPermissionService permissions, IPLMFieldVisibilityService visibility,
    IManufacturingVaExcelRenderer renderer)
    : IRequestHandler<ExportManufacturingVaQuery, OperationResult<byte[]>>
{
    private const int MaxExcelDetailRows = 1_048_575;
    public async Task<OperationResult<byte[]>> Handle(ExportManufacturingVaQuery request, CancellationToken ct)
    {
        // Fail before any query: the export includes internal formula materials, but never prices.
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders) || !visibility.CanViewFormulaMaterials())
            throw new UnauthorizedAccessException("Không có quyền xem lệnh sản xuất và NVL công thức hoặc thiếu công ty hợp lệ.");
        if (request.From?.Date > request.To?.Date || request.To?.Date == DateTime.MaxValue.Date)
            return OperationResult<byte[]>.Fail("Khoảng ngày tạo VA không hợp lệ.");
        if (request.ManufacturingFormulaIds?.Contains(Guid.Empty) == true)
            return OperationResult<byte[]>.Fail("Mã định danh VA không hợp lệ.");

        var formulas = db.ManufacturingFormulas.AsNoTracking()
            .Where(x => x.CompanyId == company && x.IsActive && EF.Functions.ILike(x.ExternalId, "VA%"));
        if (request.From is { } from) formulas = formulas.Where(x => x.CreatedDate >= from.Date);
        if (request.To is { } to)
        {
            var end = to.Date.AddDays(1);
            formulas = formulas.Where(x => x.CreatedDate < end);
        }
        if (request.ManufacturingFormulaIds is { Length: > 0 } ids)
            formulas = formulas.Where(x => ids.Contains(x.ManufacturingFormulaId));
        var headers = await formulas.OrderBy(x => x.CreatedDate).ThenBy(x => x.ExternalId).ThenBy(x => x.ManufacturingFormulaId)
            .Select(x => new VaHeader(x.ManufacturingFormulaId, x.ExternalId, x.CreatedDate)).ToListAsync(ct);
        if (headers.Count == 0) return OperationResult<byte[]>.Ok(renderer.Render([], ct));

        var items = await (
            from f in formulas
            join d in db.ManufacturingFormulaMaterials.AsNoTracking() on f.ManufacturingFormulaId equals d.ManufacturingFormulaId
            where d.IsActive
            select new VaMaterial(f.ManufacturingFormulaId, d.LineNo, d.ManufacturingFormulaMaterialId,
                d.itemType, d.MaterialId, d.ProductId,
                d.MaterialExternalIdSnapshot != null && d.MaterialExternalIdSnapshot != "" ? d.MaterialExternalIdSnapshot :
                    d.Material != null && d.Material.CompanyId == company ? d.Material.ExternalId :
                    d.Product != null && d.Product.CompanyId == company ? d.Product.SampleRequests
                        .Where(s => s.CompanyId == company && s.IsActive).OrderByDescending(s => s.CreatedDate).ThenBy(s => s.SampleRequestId)
                        .Select(s => s.ExternalId).FirstOrDefault() : null,
                d.MaterialNameSnapshot != null && d.MaterialNameSnapshot != "" ? d.MaterialNameSnapshot :
                    d.Material != null && d.Material.CompanyId == company ? d.Material.Name :
                    d.Product != null && d.Product.CompanyId == company ? d.Product.Name : null,
                d.Unit, d.Quantity)).ToListAsync(ct);

        var selections = await (
            from f in formulas
            join v in db.ProductionSelectVersions.AsNoTracking() on f.ManufacturingFormulaId equals v.ManufacturingFormulaId
            join m in db.MfgProductionOrders.AsNoTracking() on v.MfgProductionOrderId equals m.MfgProductionOrderId
            join product in db.Products.AsNoTracking().Where(p => p.CompanyId == company) on m.ProductId equals product.ProductId into products
            from p in products.DefaultIfEmpty()
            where v.CompanyId == company && m.CompanyId == company && m.IsActive
            select new VaSelection(f.ManufacturingFormulaId, m.MfgProductionOrderId, v.ValidTo,
                m.ExternalId, m.Status, m.TotalQuantity, m.StepOfProduct, m.ProductId,
                p == null ? null : p.ColourCode, p == null ? null : p.Name,
                p != null && p.Category != null && p.Category.CompanyId == company ? p.Category.ExternalId : null,
                p != null && p.Category != null && p.Category.CompanyId == company ? p.Category.Name : null)).ToListAsync(ct);
        var mfgIds = selections.Select(x => x.MfgId).Distinct().ToArray();
        var orders = await (
            from link in db.MfgOrderPOs.AsNoTracking()
            join detail in db.MerchandiseOrderDetails.AsNoTracking() on link.MerchandiseOrderDetailId equals detail.MerchandiseOrderDetailId
            join order in db.MerchandiseOrders.AsNoTracking() on detail.MerchandiseOrderId equals order.MerchandiseOrderId
            join customer in db.Customers.AsNoTracking().Where(c => c.CompanyId == company) on order.CustomerId equals customer.CustomerId into customers
            from customer in customers.DefaultIfEmpty()
            where mfgIds.Contains(link.MfgProductionOrderId) && link.IsActive && detail.IsActive && order.IsActive && order.CompanyId == company
            select new VaOrder(link.MfgProductionOrderId, order.ExternalId,
                customer == null ? null : customer.ExternalId, order.OrderType)).ToListAsync(ct);
        var productIds = selections.Select(x => x.ProductId).Distinct().ToArray();
        var productCodes = await db.SampleRequests.AsNoTracking()
            .Where(x => x.CompanyId == company && x.IsActive && productIds.Contains(x.ProductId))
            .Select(x => new VaProductCode(x.ProductId, x.ExternalId)).ToListAsync(ct);
        var lots = headers.Select(x => x.Code).Distinct().ToArray();
        var receipts = await (
            from v in warehouse.WarehouseVouchers.AsNoTracking()
            join d in warehouse.WarehouseVoucherDetails.AsNoTracking() on v.VoucherId equals d.VoucherId
            where v.CompanyId == company && v.Status == "APPLIED" && v.VoucherType == (int)RequestType.ImportFinishedGood
                && d.IsIncrease && d.IsApplied && d.QtyKg > 0 && d.LotNumber != null && lots.Contains(d.LotNumber.Trim())
            group new { v.CreatedDate, d.QtyKg, d.ProductCode } by d.LotNumber!.Trim() into g
            select new VaReceipt(g.Key, g.Sum(x => x.QtyKg), g.Max(x => x.CreatedDate), g.Select(x => x.ProductCode).Distinct().ToArray())
        ).ToListAsync(ct);
        var rows = ManufacturingVaExportMapper.Map(headers, items, selections, orders, productCodes, receipts, ct);
        if (rows.Sum(x => (long)Math.Max(1, x.Items.Count)) > MaxExcelDetailRows)
            return OperationResult<byte[]>.Fail("Báo cáo vượt giới hạn dòng Excel; hãy thu hẹp khoảng ngày tạo VA.");
        return OperationResult<byte[]>.Ok(renderer.Render(rows, ct));
    }
}
