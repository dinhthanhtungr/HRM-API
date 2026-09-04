using HRM.Domain.Enums.Logs;

namespace HRM.Application.Features.Timeline.Dtos;

public enum TimelineCreatedScope
{
    Merchandise = 0,
    Manufacturing = 1,
    Delivery = 2,
    Requisition = 3
}

public sealed class SaleOrderTimelineCardDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string PONo { get; init; } = string.Empty;
    public string? CreatedName { get; init; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerExternalId { get; init; } = string.Empty;
    /// <summary>
    /// Tổng tiền thanh toán của đơn, đã bao gồm VAT theo field <see cref="Vat"/>.
    /// </summary>
    public decimal? TotalPrice { get; set; }
    public decimal? Vat { get; init; }
    public bool IsDeliveryPaused { get; set; }
    public DateTime? DeliveryPausedFrom { get; init; }
    public DateTime? DeliveryPausedTo { get; init; }
    public string? DeliveryPauseReason { get; init; }
    public string? DeliveryPauseType { get; init; }
    public Guid? DeliveryPausedBy { get; init; }
    public bool HasComplaint { get; init; }
    public int ComplaintCount { get; init; }
    public string? LatestComplaintExternalId { get; init; }
    public string? LatestComplaintStatus { get; init; }
    public IReadOnlyList<TimelineItemDto> Details { get; set; } = Array.Empty<TimelineItemDto>();
}

public sealed class SaleOrderTimelineDetailRowDto
{
    public Guid MerchandiseOrderDetailId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string ColourCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public DateTime RequestDate { get; init; }
    public DateTime? ExpectedDate { get; init; }
    public decimal RequestQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public IReadOnlyList<DeliveryInfoDto> Deliveries { get; init; } = Array.Empty<DeliveryInfoDto>();
    public IReadOnlyList<SaleOrderComplaintInfoDto> Complaints { get; init; } = Array.Empty<SaleOrderComplaintInfoDto>();
    public IReadOnlyList<TimelineItemDto> Details { get; init; } = Array.Empty<TimelineItemDto>();
}

public sealed class SaleOrderComplaintInfoDto
{
    public Guid ComplaintReportId { get; init; }
    public Guid ComplaintReportLineId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ResolutionType { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public decimal ComplaintQuantity { get; init; }
    public decimal? ApprovedReplacementQuantity { get; init; }
    public DateTime ReportedAt { get; init; }
    public Guid? HandlingMerchandiseOrderId { get; init; }
    public string? HandlingMerchandiseOrderExternalId { get; init; }
}

public sealed class DeliveryInfoDto
{
    public string? DOExternalId { get; init; }
    public string? LotNoList { get; init; }
    public decimal QuantityDelivery { get; init; }
    public DateTime? CreatedDate { get; init; }
}
