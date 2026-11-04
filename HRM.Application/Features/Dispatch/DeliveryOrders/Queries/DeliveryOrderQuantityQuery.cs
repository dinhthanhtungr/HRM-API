using HRM.Domain.Entities.DeliverySchema;
using HRM.Application.Commons.Deliveries;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries;

/// <summary>
/// Các dòng đã phân bổ vào phiếu còn hiệu lực, dùng chung khi chọn PO và tạo/sửa phiếu.
/// Pending cũng chiếm lượng PO; hủy phiếu trả lượng đó về danh sách cần giao.
/// Đây không phải số lượng thực xuất kho hay số lượng đã được khách nhận.
/// </summary>
internal static class DeliveryOrderQuantityQuery
{
    public static IQueryable<DeliveryOrderDetail> CountedForAllocation(
        this IQueryable<DeliveryOrderDetail> details,
        Guid companyId,
        Guid? excludingDeliveryOrderId = null)
    {
        var query = details.EffectiveDeliveryLines(companyId);

        if (excludingDeliveryOrderId.HasValue)
        {
            query = query.Where(x => x.DeliveryOrderId != excludingDeliveryOrderId.Value);
        }

        return query;
    }
}
