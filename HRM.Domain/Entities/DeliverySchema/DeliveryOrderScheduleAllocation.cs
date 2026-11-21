using HRM.Domain.Entities.OrderSchema;

namespace HRM.Domain.Entities.DeliverySchema;

/// <summary>Phân bổ lượng của dòng phiếu vào đợt; ScheduleId null ghi phần ngoài lịch đã được xử lý, không chia lot/bao.</summary>
public sealed class DeliveryOrderScheduleAllocation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DeliveryOrderDetailId { get; set; }
    public Guid? MerchandiseOrderDeliveryScheduleId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DeliveryOrderDetail DeliveryOrderDetail { get; set; } = null!;
    public MerchandiseOrderDeliverySchedule? Schedule { get; set; }
}
