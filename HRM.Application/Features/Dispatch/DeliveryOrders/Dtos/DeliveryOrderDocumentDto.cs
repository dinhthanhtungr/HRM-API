namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;

/// <summary>Read model dành riêng cho chứng từ, không chứa giá vốn hoặc snapshot cost.</summary>
public sealed class DeliveryOrderDocumentDto
{
    public Guid Id { get; init; }
    public string? ExternalId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? CompanyAddress { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? CreatedDate { get; init; }
    public string? Receiver { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? Phone { get; init; }
    public string? TaxNumber { get; init; }
    public string? PaymentType { get; init; }
    public string? Note { get; init; }
    public List<string> Deliverers { get; init; } = [];
    public List<DeliveryOrderDocumentLineDto> Lines { get; init; } = [];
    public decimal TotalQuantity => Lines.Where(x => !x.IsAttach).Sum(x => x.Quantity);
    public int TotalBags => Lines.Where(x => !x.IsAttach).Sum(x => x.NumOfBags);
}

public sealed class DeliveryOrderDocumentLineDto
{
    public string? ProductCode { get; init; }
    public string? ProductName { get; init; }
    public string? LotNo { get; init; }
    public string? PONo { get; init; }
    public decimal Quantity { get; init; }
    public int NumOfBags { get; init; }
    public bool IsAttach { get; init; }
}
