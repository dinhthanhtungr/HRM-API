namespace HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;

public sealed record SaleOrderTrackingDto(
    Guid OrderId, string ExternalId, string CustomerName, string Status, DateTime CreatedDate);

public sealed record SaleOrderTrackingLineDto(
    Guid LineId, string ProductCode, string ProductName, decimal OrderedQuantity,
    decimal CompletedDeliveryQuantity, decimal RemainingQuantity,
    DateTime RequestedDate, DateTime? ExpectedDeliveryDate, DateTime? ActualDeliveryDate,
    IReadOnlyList<SaleOrderTrackingScheduleDto> Schedules);

public sealed record SaleOrderTrackingScheduleDto(decimal Quantity, DateTime RequestedDate, DateTime? PromisedDate);

public sealed record SaleOrderTrackingDetailDto(
    SaleOrderTrackingDto Order, IReadOnlyList<SaleOrderTrackingLineDto> Lines);
