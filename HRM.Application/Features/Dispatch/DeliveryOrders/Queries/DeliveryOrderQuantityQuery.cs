using HRM.Domain.Entities.DeliverySchema;

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
        var query = details.Where(x =>
            companyId != Guid.Empty &&
            x.IsActive &&
            !x.IsAttach &&
            x.MerchandiseOrderDetailId.HasValue &&
            x.DeliveryOrder.IsActive &&
            x.DeliveryOrder.CompanyId == companyId &&
            // Giữ lượng của trạng thái legacy chưa biết; chỉ giải phóng phiếu xác định đã hủy.
            (x.DeliveryOrder.Status == null ||
             (x.DeliveryOrder.Status.Trim().ToLower() != "canceled" &&
              x.DeliveryOrder.Status.Trim().ToLower() != "cancelled")));

        if (excludingDeliveryOrderId.HasValue)
        {
            query = query.Where(x => x.DeliveryOrderId != excludingDeliveryOrderId.Value);
        }

        return query;
    }
}
