using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;

internal static class DeliveryFinishReportQuery
{
    internal static IQueryable<DeliveryFinishReportSource> Project(IQueryable<DeliveryOrder> orders, Guid companyId, DateTime from, DateTime to,
        bool restricted = false)
        => orders.Where(d => companyId != Guid.Empty && d.IsActive && d.CompanyId == companyId && d.CreatedDate >= from.Date && d.CreatedDate < to.Date.AddDays(1))
            .OrderBy(d => d.CreatedDate).ThenBy(d => d.Id)
            .SelectMany(d => d.Details.Where(det => det.IsActive &&
                (!restricted || (!det.IsAttach && det.MerchandiseOrderDetail != null &&
                    det.MerchandiseOrderDetail.IsActive && det.MerchandiseOrderDetail.MerchandiseOrder.IsActive &&
                    det.MerchandiseOrderDetail.MerchandiseOrder.CustomerId == d.CustomerId &&
                    det.MerchandiseOrderDetail.MerchandiseOrder.OrderType != HRM.Domain.Enums.Merchadises.OrderType.Internal)) &&
                (det.MerchandiseOrderDetail == null || det.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == companyId)), (d, det) => new DeliveryFinishReportSource
            {
                DeliveryOrderId = d.Id,
                DeliveryLineId = det.Id,
                DeliveryCustomerId = d.CustomerId,
                CustomerId = det.MerchandiseOrderDetail != null ? det.MerchandiseOrderDetail.MerchandiseOrder.CustomerId : d.CustomerId,
                OrderId = det.MerchandiseOrderDetail != null ? det.MerchandiseOrderDetail.MerchandiseOrderId : null,
                ProductId = det.ProductId,
                CreatedDate = d.CreatedDate!.Value,
                DetailId = det.MerchandiseOrderDetailId,
                Row = new DeliveryFinishRow
                {
                    DeliveryExternalId = d.ExternalId,
                    OrderExternalId = det.MerchandiseOrderDetail != null ? det.MerchandiseOrderDetail.MerchandiseOrder.ExternalId : null,
                    OrderCreatedDate = det.MerchandiseOrderDetail != null ? det.MerchandiseOrderDetail.MerchandiseOrder.CreateDate : null,
                    DeliveryRequestDate = det.MerchandiseOrderDetail != null ? det.MerchandiseOrderDetail.DeliveryRequestDate : null,
                    DeliveryActualDate = d.CreatedDate,
                    CustomerName = det.MerchandiseOrderDetail != null ? det.MerchandiseOrderDetail.MerchandiseOrder.CustomerNameSnapshot : d.CustomerExternalIdSnapShot,
                    DelivererName = d.Deliverers.OrderBy(x => x.DelivererInfor.Name).Select(x => x.DelivererInfor.Name).FirstOrDefault(),
                    ProductDisplay = (det.ProductExternalIdSnapShot ?? "") + " - " + (det.ProductNameSnapShot ?? ""),
                    WarehouseDisplay = "", LotNoOrBatch = restricted ? null : det.LotConsumptions.Any(l => l.IsActive)
                        ? string.Join(", ", det.LotConsumptions.Where(l => l.IsActive).OrderBy(l => l.LotNo).Select(l => l.LotNo)) : det.LotNoList,
                    QuantityKg = det.Quantity, NumOfBags = det.NumOfBags,
                    PoNo = restricted ? null : det.PONo, Note = restricted ? null : d.Note
                }
            });

    public static async Task<List<DeliveryFinishRow>> BuildDeliveryFinishRowsAsync(IDispatchReadDbContext db, Guid companyId,
        DateTime from, DateTime to, CancellationToken ct)
    {
        var data = await Project(db.DeliveryOrders.AsNoTracking(), companyId, from, to).ToListAsync(ct);

        var ids = data.Where(x => x.DetailId.HasValue).Select(x => x.DetailId!.Value).Distinct().ToArray();

        var dates = await db.MfgOrderPOs.AsNoTracking()
            .Where(x => x.IsActive && ids.Contains(x.MerchandiseOrderDetailId) &&
            x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == companyId && x.Detail.MerchandiseOrder.CompanyId == companyId)
            .GroupBy(x => x.MerchandiseOrderDetailId).Select(g => new { Id = g.Key, Date = g.Max(x => x.ProductionOrder.ExpectedDate) })
            .ToDictionaryAsync(x => x.Id, x => x.Date, ct);
        
        foreach (var row in data)
            row.Row.ExpectedDeliveryDate = row.DetailId.HasValue ? dates.GetValueOrDefault(row.DetailId.Value) : null;
        return data.Select(x => x.Row).ToList();
    }
}

internal sealed class DeliveryFinishReportSource
{
    public Guid DeliveryOrderId { get; init; }
    public Guid DeliveryLineId { get; init; }
    public Guid DeliveryCustomerId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? OrderId { get; init; }
    public Guid? ProductId { get; init; }
    public DateTime CreatedDate { get; init; }
    public Guid? DetailId { get; init; }
    public DeliveryFinishRow Row { get; init; } = new();
}
