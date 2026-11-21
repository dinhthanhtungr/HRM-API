namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

/// <summary>Input đã scope theo detail/company; allocation gồm phiếu Pending còn hiệu lực.</summary>
public sealed record DeliveryScheduleCapacity(Guid ScheduleId, decimal Quantity, decimal AllocatedQuantity,
    DateOnly RequestedDate, DateOnly? PromisedDate);
public sealed record DeliveryScheduleAllocationPart(Guid ScheduleId, decimal Quantity);
public sealed record DeliveryScheduleAllocationPlan(IReadOnlyList<DeliveryScheduleAllocationPart> Items,
    decimal UnscheduledQuantity);

/// <summary>Gợi ý phân bổ một dòng phiếu vào nhiều đợt. Không tách sản phẩm/lot/bao và không ghi DB.</summary>
public static class DeliveryScheduleAllocationRules
{
    public static DeliveryScheduleAllocationPlan Suggest(decimal quantity,
        IReadOnlyList<DeliveryScheduleCapacity> schedules, Guid? preferredScheduleId = null)
    {
        Validate(quantity, schedules);
        if (preferredScheduleId.HasValue && schedules.All(x => x.ScheduleId != preferredScheduleId.Value))
            throw new ArgumentException("Đợt ưu tiên không thuộc dòng đơn hàng.", nameof(preferredScheduleId));
        var remaining = quantity;
        var parts = new List<DeliveryScheduleAllocationPart>();
        foreach (var schedule in schedules.OrderBy(x => x.ScheduleId == preferredScheduleId ? 0 : 1)
            .ThenBy(x => x.PromisedDate ?? x.RequestedDate).ThenBy(x => x.RequestedDate).ThenBy(x => x.ScheduleId))
        {
            var available = Math.Max(0m, schedule.Quantity - schedule.AllocatedQuantity);
            var allocated = Math.Min(remaining, available);
            if (allocated > 0m)
            {
                parts.Add(new(schedule.ScheduleId, allocated));
                remaining -= allocated;
            }
            if (remaining == 0m) break;
        }
        // Giữ rule giao vượt PO; không âm thầm tăng capacity một đợt.
        return new(parts, remaining);
    }

    public static DeliveryScheduleAllocationPlan ValidateManual(decimal quantity,
        IReadOnlyList<DeliveryScheduleCapacity> schedules, IReadOnlyList<DeliveryScheduleAllocationPart> parts)
    {
        Validate(quantity, schedules);
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count > schedules.Count || parts.Any(x => x is null))
            throw new ArgumentException("Phân bổ đợt không hợp lệ.", nameof(parts));
        if (parts.Select(x => x.ScheduleId).Distinct().Count() != parts.Count)
            throw new ArgumentException("Một đợt bị phân bổ trùng.", nameof(parts));
        foreach (var part in parts)
        {
            var schedule = schedules.SingleOrDefault(x => x.ScheduleId == part.ScheduleId);
            if (schedule is null || part.Quantity <= 0m || decimal.Round(part.Quantity, 3) != part.Quantity)
                throw new ArgumentException("Phân bổ đợt không hợp lệ.", nameof(parts));
            if (part.Quantity > Math.Max(0m, schedule.Quantity - schedule.AllocatedQuantity))
                throw new ArgumentException("Phân bổ vượt lượng còn lại của đợt.", nameof(parts));
        }
        var allocated = parts.Sum(x => x.Quantity);
        if (allocated > quantity)
            throw new ArgumentException("Tổng phân bổ vượt lượng trên dòng phiếu.", nameof(parts));
        return new(parts.ToArray(), quantity - allocated);
    }

    private static void Validate(decimal quantity, IReadOnlyList<DeliveryScheduleCapacity> schedules)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        if (schedules.Any(x => x is null)) throw new ArgumentException("Capacity lịch giao không hợp lệ.", nameof(schedules));
        if (quantity <= 0m || decimal.Round(quantity, 3) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (schedules.Select(x => x.ScheduleId).Distinct().Count() != schedules.Count
            || schedules.Any(x => x.ScheduleId == Guid.Empty || x.Quantity <= 0m || x.AllocatedQuantity < 0m
                || decimal.Round(x.Quantity, 3) != x.Quantity || decimal.Round(x.AllocatedQuantity, 3) != x.AllocatedQuantity
                || x.RequestedDate == default || x.PromisedDate == default(DateOnly)))
            throw new ArgumentException("Capacity lịch giao không hợp lệ.", nameof(schedules));
    }
}
