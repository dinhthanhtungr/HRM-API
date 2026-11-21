namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;

public sealed record DeliveryTrackingDto(
    Guid Id, string? ExternalId, string CustomerName, string Status, DateTime? CreatedDate);

public sealed record DeliveryTrackingLineDto(Guid Id, string? ProductCode, string? ProductName, decimal Quantity);

public sealed record DeliveryTrackingDetailDto(
    DeliveryTrackingDto Delivery, IReadOnlyList<DeliveryTrackingLineDto> Lines);
