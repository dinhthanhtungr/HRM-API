using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

/// <summary>Ghi phân bổ cùng transaction phiếu; vẫn chạy khi ẩn UI nhưng StorageReady=true.</summary>
public sealed class DeliveryScheduleFulfillmentService(IDeliveryScheduleDbContext db, IOptions<DeliveryScheduleOptions> options)
{
    public async Task<IReadOnlyList<DeliveryScheduleAllocationPart>?> ExistingPlanAsync(Guid deliveryId, Guid detailId, decimal quantity, Guid company, CancellationToken ct)
    {
        var lines = db.DeliveryOrderDetails.AsNoTracking().CountedForAllocation(company)
            .Where(x => x.DeliveryOrderId == deliveryId && x.MerchandiseOrderDetailId == detailId);
        var previousQuantity = await lines.SumAsync(x => (decimal?)x.Quantity, ct) ?? 0;
        if (previousQuantity != quantity) return null;
        var recorded = db.DeliveryOrderScheduleAllocations.AsNoTracking().Where(a => a.IsActive && lines.Any(line => line.Id == a.DeliveryOrderDetailId));
        var parts = await recorded.Where(a => a.Schedule != null && a.MerchandiseOrderDeliveryScheduleId.HasValue && a.Schedule.MerchandiseOrderDetailId == detailId)
            .GroupBy(a => a.MerchandiseOrderDeliveryScheduleId!.Value)
            .Select(g => new DeliveryScheduleAllocationPart(g.Key, g.Sum(x => x.Quantity))).ToArrayAsync(ct);
        return await recorded.AnyAsync(ct) ? parts : null;
    }
    public async Task<OperationResult> AllocateAsync(DeliveryOrder order, IReadOnlyCollection<DeliveryOrderLineRequest> requests,
        Guid company, Guid employee, DateTime now, CancellationToken ct)
    {
        if (!options.Value.StorageReady)
            return requests.Any(x => x.ScheduleAllocations is not null)
                ? OperationResult.Fail("Lưu trữ phân bổ đợt chưa bật.") : OperationResult.Ok();
        var lines = order.Details.Where(x => x.IsActive && !x.IsAttach && x.MerchandiseOrderDetailId.HasValue).ToArray();
        var oldIds = await db.DeliveryOrderDetails.AsNoTracking().Where(x => x.DeliveryOrderId == order.Id && x.DeliveryOrder.CompanyId == company)
            .Select(x => x.MerchandiseOrderDetailId).ToListAsync(ct);
        var ids = lines.Select(x => x.MerchandiseOrderDetailId!.Value).Concat(oldIds.Where(x => x.HasValue).Select(x => x!.Value)).Distinct().Order().ToArray();
        foreach (var id in ids) await db.LockDeliveryScheduleAsync(company, id, ct);
        var valid = await db.MerchandiseOrderDetails.AsNoTracking().Where(x => ids.Contains(x.MerchandiseOrderDetailId)
            && x.MerchandiseOrder.CompanyId == company).Select(x => x.MerchandiseOrderDetailId).ToArrayAsync(ct);
        if (valid.Length != ids.Length) return OperationResult.Fail("Dòng đơn không thuộc công ty.");
        var counted = db.DeliveryOrderDetails.AsNoTracking().CountedForAllocation(company, order.Id);
        foreach (var line in lines)
        {
            var detailId = line.MerchandiseOrderDetailId!.Value;
            var schedules = await db.MerchandiseOrderDeliverySchedules.AsNoTracking().Where(x => x.MerchandiseOrderDetailId == detailId && x.IsActive).ToListAsync(ct);
            var used = await db.DeliveryOrderScheduleAllocations.AsNoTracking()
                .Where(a => a.IsActive && a.Schedule != null && a.MerchandiseOrderDeliveryScheduleId.HasValue && a.Schedule.MerchandiseOrderDetailId == detailId
                    && counted.Any(d => d.Id == a.DeliveryOrderDetailId && d.MerchandiseOrderDetailId == detailId))
                .GroupBy(a => a.MerchandiseOrderDeliveryScheduleId!.Value)
                .Select(g => new { Id = g.Key, Quantity = g.Sum(a => a.Quantity) }).ToDictionaryAsync(x => x.Id, x => x.Quantity, ct);
            var capacities = schedules.Select(x => new DeliveryScheduleCapacity(x.MerchandiseOrderDeliveryScheduleId, x.Quantity,
                used.GetValueOrDefault(x.MerchandiseOrderDeliveryScheduleId), DateOnly.FromDateTime(x.RequestedDate),
                x.PromisedDate.HasValue ? DateOnly.FromDateTime(x.PromisedDate.Value) : null)).ToArray();
            var input = requests.Single(x => x.MerchandiseOrderDetailId == detailId);
            var existingPlan = input.ScheduleAllocations ?? await ExistingPlanAsync(order.Id, detailId, line.Quantity, company, ct);
            DeliveryScheduleAllocationPlan plan;
            try
            {
                plan = existingPlan is null ? DeliveryScheduleAllocationRules.Suggest(line.Quantity, capacities)
                    : DeliveryScheduleAllocationRules.ValidateManual(line.Quantity, capacities, existingPlan);
            }
            catch (ArgumentException ex) { return OperationResult.Fail(ex.Message); }
            foreach (var part in plan.Items)
                db.DeliveryOrderScheduleAllocations.Add(new() { DeliveryOrderDetailId = line.Id,
                    MerchandiseOrderDeliveryScheduleId = part.ScheduleId, Quantity = part.Quantity, CreatedBy = employee, CreatedDate = now });
            if (plan.UnscheduledQuantity > 0)
                db.DeliveryOrderScheduleAllocations.Add(new() { DeliveryOrderDetailId = line.Id, MerchandiseOrderDeliveryScheduleId = null,
                    Quantity = plan.UnscheduledQuantity, CreatedBy = employee, CreatedDate = now });
        }
        return OperationResult.Ok();
    }
}
