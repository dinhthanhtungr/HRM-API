namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class DeliveryOrderListItemDto
{
    public Guid Id { get; init; }
    public string? ExternalId { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string? CustomerExternalIdSnapshot { get; init; }
    public string? CustomerName { get; init; }
    public string? MerchandiseOrderExternalIds { get; init; }
    public string? DelivererNames { get; init; }
    public string? PaymentDeadline { get; init; }
    public DateTime? CreatedDate { get; init; }
    public string? Note { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<DeliveryOrderLineDto> Lines { get; init; } = Array.Empty<DeliveryOrderLineDto>();
}
