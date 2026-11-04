namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

public sealed class PurchaseOrderReceiptProgressDto
{
    public Guid PurchaseOrderId { get; init; }
    public string PurchaseOrderCode { get; init; } = "";
    public string? OrderStatus { get; init; }
    public bool IsActive { get; init; }
    public string AssociationStatus { get; set; } = "Matched";
    public List<string> Warnings { get; init; } = [];
    public List<ReceiptMaterialProgressDto> Materials { get; set; } = [];
    public List<ReceiptRequestDto> Requests { get; set; } = [];
    public List<ReceiptVoucherLineDto> Receipts { get; set; } = [];
}

public sealed class ReceiptMaterialProgressDto
{
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string? Unit { get; init; }
    public List<Guid> PurchaseOrderDetailIds { get; init; } = [];
    public decimal OrderedQuantity { get; init; }
    public decimal? RecordedQuantity { get; set; }
    public decimal? PostedQuantity { get; set; }
    public decimal? RemainingToPost { get; set; }
    public decimal? OverPostedQuantity { get; set; }
    public decimal? PostingPercent { get; set; }
    public bool RequiresReview { get; set; }
    public decimal? NotInspectedQuantity { get; set; }
    public decimal? OnHoldQuantity { get; set; }
    public decimal? PassedQuantity { get; set; }
    public decimal? FailedQuantity { get; set; }
    public decimal? SpecialAcceptedQuantity { get; set; }
    public decimal? UnknownQcQuantity { get; set; }
}

public sealed record ReceiptRequestDto(int RequestId, string RequestCode, int RequestType, int RequestStatus, DateTime CreatedDate);

public sealed class ReceiptVoucherLineDto
{
    public int RequestId { get; init; }
    public string RequestCode { get; init; } = "";
    public long VoucherId { get; init; }
    public string VoucherCode { get; init; } = "";
    public string? VoucherStatus { get; init; }
    public DateTime VoucherDate { get; init; }
    public long VoucherDetailId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string? LotNumber { get; init; }
    public string Unit { get; init; } = "";
    public decimal RecordedQuantity { get; init; }
    public bool IsApplied { get; init; }
    public int DetailType { get; init; }
    public string? SlotCode { get; init; }
    public bool Included { get; set; } = true;
    public string QcStatus { get; set; } = "NotInspected";
    public Guid? QcId { get; set; }
    public DateTime? QcDate { get; set; }
    public decimal PostedQuantity { get; set; }
    public DateTime? LastPostingDate { get; set; }
    public List<ReceiptPostingDto> Postings { get; set; } = [];
    public List<string> Warnings { get; init; } = [];
}

// Signed movements are grouped by location, lot and stock type; they are NOT separate deliveries.
public sealed record ReceiptPostingDto(int SlotId, string? SlotCode, string? LotNumber, int? StockType, decimal NetQuantity);
