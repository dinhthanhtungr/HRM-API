namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class SelectableDeliveryOrderDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string PONo { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerNameSnapshot { get; init; } = string.Empty;
    public string CustomerExternalIdSnapshot { get; init; } = string.Empty;
    public string PhoneSnapshot { get; init; } = string.Empty;
    public string? Receiver { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentType { get; init; }
    public string? Status { get; init; }
    public string? Currency { get; init; }
    public string? Note { get; init; }
    /// <summary>Ngày hứa giao MFG muộn nhất của PO; null khi chưa có ngày trên liên kết hợp lệ.</summary>
    public DateTime? PromisedDeliveryDate { get; init; }
    public bool IsDeliveryPaused { get; init; }
    public DateTime? DeliveryPausedFrom { get; init; }
    public DateTime? DeliveryPausedTo { get; init; }
    public string? DeliveryPauseReason { get; init; }
    public string? DeliveryPauseType { get; init; }
    public string? DeliveryPausedName { get; init; }
    public IReadOnlyList<SelectableDeliveryOrderLineDto> Lines { get; init; } = Array.Empty<SelectableDeliveryOrderLineDto>();
}
