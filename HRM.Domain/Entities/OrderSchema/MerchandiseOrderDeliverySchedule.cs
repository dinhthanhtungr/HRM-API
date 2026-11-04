namespace HRM.Domain.Entities.OrderSchema;

/// <summary>
/// Một đợt giao của detail, dùng chung khối lượng cho ngày yêu cầu và ngày hứa giao.
/// </summary>
public class MerchandiseOrderDeliverySchedule
{
    public Guid MerchandiseOrderDeliveryScheduleId { get; set; }
    public Guid MerchandiseOrderDetailId { get; set; }

    public decimal Quantity { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime? PromisedDate { get; set; }
    public string? RequestNote { get; set; }
    public string? PromiseNote { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; }
    public Guid UpdatedBy { get; set; }

    public virtual MerchandiseOrderDetail MerchandiseOrderDetail { get; set; } = null!;
}
