using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Deliveries;
using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;

internal sealed class GetSaleOrderTrackingQueryHandler(
    SaleOrderTrackingReader reader,
    IPLMReadDbContext db,
    IDeliveryScheduleDbContext schedulesDb,
    ICurrentUser user,
    IOptions<DeliveryScheduleOptions> scheduleOptions)
    : IRequestHandler<GetSaleOrderTrackingQuery, SaleOrderTrackingDetailDto?>
{
    public async Task<SaleOrderTrackingDetailDto?> Handle(GetSaleOrderTrackingQuery request, CancellationToken cancellationToken)
    {
        var source = reader.Read();
        if (source is null) return null;

        var query = source.Where(order => order.MerchandiseOrderId == request.OrderId);
        var header = await SaleOrderTrackingReader.Project(query).SingleOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        var completedDeliveries = db.DeliveryOrderDetails.AsNoTracking()
            .EffectiveDeliveryLines(user.CompanyId.GetValueOrDefault())
            .Where(line => line.DeliveryOrder.Status != null &&
                line.DeliveryOrder.Status.Trim().ToLower() == "completed");
        var details = query.SelectMany(order => order.MerchandiseOrderDetails).Where(line => line.IsActive);
        var rows = await details.OrderBy(line => line.MerchandiseOrderDetailId)
            .Select(line => new
            {
                line.MerchandiseOrderDetailId,
                line.ProductExternalIdSnapshot,
                line.ProductNameSnapshot,
                line.ExpectedQuantity,
                CompletedQuantity = completedDeliveries
                    .Where(delivery => delivery.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId)
                    .Sum(delivery => (decimal?)delivery.Quantity) ?? 0m,
                line.DeliveryRequestDate,
                line.ExpectedDeliveryDate,
                line.DeliveryActualDate,
            }).ToListAsync(cancellationToken);

        var schedulesByLine = new Dictionary<Guid, IReadOnlyList<SaleOrderTrackingScheduleDto>>();
        if (scheduleOptions.Value.StorageReady && rows.Count > 0)
        {
            var lineIds = details.Select(line => line.MerchandiseOrderDetailId);
            var schedules = await schedulesDb.MerchandiseOrderDeliverySchedules.AsNoTracking()
                .Where(schedule => schedule.IsActive && lineIds.Contains(schedule.MerchandiseOrderDetailId))
                .OrderBy(schedule => schedule.RequestedDate).ThenBy(schedule => schedule.MerchandiseOrderDeliveryScheduleId)
                .Select(schedule => new
                {
                    schedule.MerchandiseOrderDetailId,
                    Tracking = new SaleOrderTrackingScheduleDto(schedule.Quantity, schedule.RequestedDate, schedule.PromisedDate),
                }).ToListAsync(cancellationToken);
            schedulesByLine = schedules.GroupBy(schedule => schedule.MerchandiseOrderDetailId)
                .ToDictionary(group => group.Key,
                    group => (IReadOnlyList<SaleOrderTrackingScheduleDto>)group.Select(schedule => schedule.Tracking).ToArray());
        }

        var lines = rows.Select(line => new SaleOrderTrackingLineDto(
            line.MerchandiseOrderDetailId, line.ProductExternalIdSnapshot, line.ProductNameSnapshot,
            line.ExpectedQuantity, line.CompletedQuantity, Math.Max(0m, line.ExpectedQuantity - line.CompletedQuantity),
            line.DeliveryRequestDate, line.ExpectedDeliveryDate, line.DeliveryActualDate,
            schedulesByLine.GetValueOrDefault(line.MerchandiseOrderDetailId) ?? [])).ToArray();
        return new(header, lines);
    }
}
