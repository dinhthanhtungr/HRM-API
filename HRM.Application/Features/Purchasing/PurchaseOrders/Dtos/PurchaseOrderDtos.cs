namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

public sealed record PurchaseOrderLineRequest
{
    public Guid MaterialId { get; init; }
    public decimal Quantity { get; init; }
    public string? Package { get; init; }
    public decimal UnitPriceAgreed { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? Note { get; init; }
}

/// <summary>Client không được gửi company, audit, trạng thái hay mã PO.</summary>
public sealed record CreatePurchaseOrderRequest
{
    public Guid SupplierId { get; init; }
    public string? OrderType { get; init; }
    public DateTime? RequestDeliveryDate { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentTypes { get; init; }
    public int? Vat { get; init; }
    public string? Comment { get; init; }
    public string? PlpuComment { get; init; }
    public IReadOnlyCollection<Guid> MerchandiseOrderIds { get; init; } = [];
    public IReadOnlyCollection<PurchaseOrderLineRequest> Items { get; init; } = [];
}

public sealed record CancelPurchaseOrderRequest(string? Reason);

/// <summary>PATCH ghi chú: field bỏ qua được giữ nguyên; đưa tên field vào ClearFields để xóa.</summary>
public sealed record UpdatePurchaseOrderNotesRequest
{
    public string? Comment { get; init; }
    public string? PlpuComment { get; init; }
    public IReadOnlyCollection<string> ClearFields { get; init; } = [];
}

public class PurchaseOrderListItemDto
{
    public Guid PurchaseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateTime? RequestDeliveryDate { get; init; }
    public decimal TotalPrice { get; init; }
    public decimal RealTotalPrice { get; set; }
    public DateTime? CreateDate { get; init; }
    public DateTime? RealDeliveryDate { get; set; }
    public string MerchandiseOrderCodes { get; init; } = string.Empty;
}

public sealed class PurchaseOrderDetailDto : PurchaseOrderListItemDto
{
    public string? OrderType { get; init; }
    public string? Comment { get; init; }
    public string? PlpuComment { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentTypes { get; init; }
    public int? Vat { get; init; }
    public IReadOnlyList<PurchaseOrderLineDto> Items { get; init; } = [];
}

public sealed class PurchaseOrderLineDto
{
    public Guid PurchaseOrderDetailId { get; init; }
    public int LineNo { get; init; }
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal? RealQuantity { get; set; }
    public string? Package { get; init; }
    public decimal UnitPriceAgreed { get; init; }
    public decimal TotalPriceAgreed { get; init; }
    public decimal? BaseCostSnapshot { get; init; }
    public DateTime? BaseDateSnapshot { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? Note { get; init; }
}

public sealed class PurchaseOrderFileDto
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public byte[] Content { get; init; } = [];
}
