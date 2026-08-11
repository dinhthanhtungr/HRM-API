using HRM.Application.Features.Dispatch.Deliverers.Dtos;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

public sealed class DeliveryOrderDetailDto
{
    public Guid Id { get; init; }
    public string? ExternalId { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid CompanyId { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerExternalIdSnapshot { get; init; }
    public string? CustomerName { get; init; }
    public string? MerchandiseOrderExternalIds { get; init; }

    public string? Receiver { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentType { get; init; }
    public string? PaymentDeadline { get; init; }
    public string? TaxNumber { get; init; }
    public string? PhoneSnapshot { get; init; }
    public bool? RequiresUnloading { get; init; }
    public decimal? DeliveryPrice { get; init; }
    public string? Note { get; init; }
    public bool IsActive { get; init; }
    public Guid CreatedBy { get; init; }
    public DateTime? CreatedDate { get; init; }
    public Guid? UpdatedBy { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public bool CanEdit { get; init; }
    public int LineCount { get; init; }
    public decimal TotalQuantity { get; init; }
    public int TotalNumOfBags { get; init; }

    public IReadOnlyList<DeliveryOrderLineDto> Lines { get; init; } = Array.Empty<DeliveryOrderLineDto>();
    public IReadOnlyList<DelivererDto> Deliverers { get; init; } = Array.Empty<DelivererDto>();
}
