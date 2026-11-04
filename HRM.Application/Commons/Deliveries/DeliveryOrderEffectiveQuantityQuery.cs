using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Application.Commons.Deliveries;

/// <summary>Lượng phiếu còn hiệu lực dùng chung cho kế hoạch, timeline và phân bổ PO.</summary>
internal static class DeliveryOrderEffectiveQuantityQuery
{
    internal static IQueryable<DeliveryOrderDetail> EffectiveDeliveryLines(this IQueryable<DeliveryOrderDetail> details, Guid companyId)
        => details.Where(x => companyId != Guid.Empty && x.IsActive && !x.IsAttach &&
            x.MerchandiseOrderDetailId.HasValue && x.DeliveryOrder.IsActive && x.DeliveryOrder.CompanyId == companyId &&
            (x.DeliveryOrder.Status == null ||
             (x.DeliveryOrder.Status.Trim().ToLower() != "canceled" && x.DeliveryOrder.Status.Trim().ToLower() != "cancelled")));
}
