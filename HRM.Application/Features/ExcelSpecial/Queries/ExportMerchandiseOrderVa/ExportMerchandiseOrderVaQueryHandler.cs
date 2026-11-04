using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportMerchandiseOrderVa;

internal sealed class ExportMerchandiseOrderVaQueryHandler(
    IDispatchReadDbContext db,
    ICurrentUser user,
    IMerchandiseOrderVaExcelRenderer renderer)
    : IRequestHandler<ExportMerchandiseOrderVaQuery, OperationResult<byte[]>>
{
    public async Task<OperationResult<byte[]>> Handle(ExportMerchandiseOrderVaQuery request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanRead(user) || user.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<byte[]>.Fail("Bạn không có quyền đọc phiếu giao hàng hoặc chưa có công ty hợp lệ.");
        if (request.From?.Date > request.To?.Date || request.To?.Date == DateTime.MaxValue.Date)
            return OperationResult<byte[]>.Fail("Khoảng ngày không hợp lệ.");
        if (request.MerchandiseOrderIds?.Contains(Guid.Empty) == true)
            return OperationResult<byte[]>.Fail("Mã định danh đơn hàng không hợp lệ.");

        var orders = db.MerchandiseOrders.AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == companyId);
        if (request.From is { } from)
            orders = orders.Where(x => x.CreateDate >= from.Date);
        if (request.To is { } to)
        {
            var end = to.Date.AddDays(1);
            orders = orders.Where(x => x.CreateDate < end);
        }
        if (request.MerchandiseOrderIds is { Length: > 0 } ids)
            orders = orders.Where(x => ids.Contains(x.MerchandiseOrderId));

        var headers = await orders.OrderByDescending(x => x.CreateDate).ThenBy(x => x.MerchandiseOrderId)
            .Select(x => new { x.MerchandiseOrderId, x.ExternalId }).ToListAsync(ct);
        var details = await (
            from order in orders
            join detail in db.MerchandiseOrderDetails.AsNoTracking() on order.MerchandiseOrderId equals detail.MerchandiseOrderId
            where detail.IsActive
            orderby detail.ExpectedDeliveryDate, detail.MerchandiseOrderDetailId
            select new
            {
                order.MerchandiseOrderId,
                detail.MerchandiseOrderDetailId,
                detail.ProductId,
                ColourCode = detail.Product != null && detail.Product.CompanyId == companyId
                    ? detail.Product.ColourCode ?? detail.Product.Code ?? detail.ProductExternalIdSnapshot
                    : detail.ProductExternalIdSnapshot
            }).ToListAsync(ct);
        var lines = await (
            from order in orders
            join detail in db.MerchandiseOrderDetails.AsNoTracking() on order.MerchandiseOrderId equals detail.MerchandiseOrderId
            join line in db.DeliveryOrderDetails.AsNoTracking() on detail.MerchandiseOrderDetailId equals line.MerchandiseOrderDetailId
            where detail.IsActive && line.IsActive && !line.IsAttach && line.DeliveryOrder.IsActive
                && line.DeliveryOrder.CompanyId == companyId
                && line.DeliveryOrder.Status != "Canceled" && line.DeliveryOrder.Status != "Cancelled"
            select new
            {
                detail.MerchandiseOrderDetailId,
                line.LotNoList,
                Lots = line.LotConsumptions.Where(x => x.IsActive).Select(x => x.LotNo).ToList()
            }).ToListAsync(ct);

        // Snapshot lot trên phiếu là nguồn dữ liệu; không resolve VA đang chọn của MFG.
        var lotsByDetail = lines.GroupBy(x => x.MerchandiseOrderDetailId).ToDictionary(g => g.Key,
            g => g.SelectMany(x => x.Lots.Count > 0 ? x.Lots : SplitLegacyLots(x.LotNoList))
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
        var rows = new List<MerchandiseOrderVaExcelRow>();
        var detailsByOrder = details.ToLookup(x => x.MerchandiseOrderId);
        foreach (var order in headers)
        {
            if (!detailsByOrder.Contains(order.MerchandiseOrderId))
                rows.Add(new(order.ExternalId, null, null));
            foreach (var product in detailsByOrder[order.MerchandiseOrderId].GroupBy(x => x.ProductId))
            {
                var colourCode = product.First().ColourCode;
                var lots = product.SelectMany(detail => lotsByDetail.GetValueOrDefault(detail.MerchandiseOrderDetailId) ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                if (lots.Length == 0)
                    rows.Add(new(order.ExternalId, colourCode, null));
                else
                    rows.AddRange(lots.Select(lot => new MerchandiseOrderVaExcelRow(order.ExternalId, colourCode, lot)));
            }
        }
        ct.ThrowIfCancellationRequested();
        return OperationResult<byte[]>.Ok(renderer.Render(rows));
    }

    private static IEnumerable<string> SplitLegacyLots(string? value) => (value ?? string.Empty)
        .Split([',', ';', '|', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
}
