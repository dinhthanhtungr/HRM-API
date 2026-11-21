using HRM.Application.Commons.Models;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;

/// <summary>Activates the selected VA when entering Scheduling. Caller loads tracked,
/// active, company-scoped selections and owns SaveChanges and the transaction.</summary>
internal static class ManufacturingSchedulingSelectionRules
{
    public static OperationResult Activate(IReadOnlyCollection<ProductionSelectVersion> selections, DateTime now)
    {
        var effective = selections.Where(x => x.ValidFrom.HasValue).ToArray();
        var candidates = effective.Length > 0 ? effective : selections.ToArray();
        if (candidates.Length != 1)
            return OperationResult.Fail(candidates.Length == 0
                ? "Không thể chuyển Chờ lịch: lệnh chưa có VA active được gắn."
                : "Không thể chuyển Chờ lịch: có nhiều VA đang mở, không xác định được VA cần kích hoạt.");
        candidates[0].ValidFrom ??= now;
        return OperationResult.Ok();
    }
}
