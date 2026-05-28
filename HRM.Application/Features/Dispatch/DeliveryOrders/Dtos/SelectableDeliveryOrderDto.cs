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
    public IReadOnlyList<SelectableDeliveryOrderLineDto> Lines { get; init; } = Array.Empty<SelectableDeliveryOrderLineDto>();
}
