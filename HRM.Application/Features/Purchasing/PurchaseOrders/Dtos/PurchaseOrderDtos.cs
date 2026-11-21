using HRM.Application.Features.Purchasing.PurchaseOrders.Services;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;

public sealed record PurchaseOrderLineRequest
{
    public Guid MaterialId { get; init; }
    public decimal Quantity { get; init; }
    public string? Package { get; init; }
    [System.ComponentModel.DataAnnotations.Required]
    public decimal? UnitPriceAgreed { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public string? Note { get; init; }
}

/// <summary>Client không được gửi company, audit, trạng thái hay mã PO.</summary>
public sealed record CreatePurchaseOrderRequest
{
    public Guid SupplierId { get; init; }
    [System.ComponentModel.DataAnnotations.Required]
    public string? OrderType { get; init; }
    [System.ComponentModel.DataAnnotations.Required]
    public DateTime? RequestDeliveryDate { get; init; }
    /// <summary>Được nhận để tương thích client cũ nhưng không lưu. Ngày thực tế do IQC xác định.</summary>
    public DateTime? RealDeliveryDate { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentTypes { get; init; }
    public int? Vat { get; init; }
    public string? Comment { get; init; }
    public string? PlpuComment { get; init; }
    public IReadOnlyCollection<Guid> MerchandiseOrderIds { get; init; } = [];
    public IReadOnlyCollection<PurchaseOrderLineRequest> Items { get; init; } = [];
}

public sealed record CancelPurchaseOrderRequest(string? Reason);

/// <summary>PATCH thông tin PO Pending: field thiếu/null giữ nguyên; ClearFields chỉ nhận các field nullable đã công bố.</summary>
public sealed record UpdatePurchaseOrderNotesRequest
{
    public string? OrderType { get; init; }
    public DateTime? RequestDeliveryDate { get; init; }
    /// <summary>Bỏ qua cả value và clearFields tương ứng; không cập nhật ngày từ client.</summary>
    public DateTime? RealDeliveryDate { get; init; }
    public string? PaymentTypes { get; init; }
    public int? Vat { get; init; }
    public string? DeliveryAddress { get; init; }
    public bool? IsActive { get; init; }
    public string? Comment { get; init; }
    public string? PlpuComment { get; init; }
    public IReadOnlyCollection<string> ClearFields { get; init; } = [];
}

public class PurchaseOrderListItemDto
{
    public PurchaseReturnProgressDto? ReturnProgress { get; set; }
    public Guid PurchaseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public string? SupplierExternalId { get; init; }
    public string? OrderType { get; init; }
    public string? Comment { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<PurchaseOrderLineDto> Details { get; init; } = [];
    public decimal TotalAmount => TotalPrice;
    public decimal RealTotalAmount => RealTotalPrice;
    public string MerchediseListExternalId => MerchandiseOrderCodes;
    public DateTime? RequestDeliveryDate { get; init; }
    public decimal Subtotal { get; init; }
    public int? Vat { get; init; }
    public decimal VatAmount => PurchaseOrderTotals.VatAmount(Subtotal, Vat);
    public decimal CalculatedTotalPrice => PurchaseOrderTotals.Calculate(Subtotal, Vat);
    public decimal? StoredTotalPrice { get; init; }
    public decimal TotalPrice => StoredTotalPrice ?? CalculatedTotalPrice;
    public bool HasTotalPriceMismatch => StoredTotalPrice.HasValue && StoredTotalPrice.Value != CalculatedTotalPrice;
    public decimal RealTotalPrice { get; set; }
    public DateTime? CreateDate { get; init; }
    public DateTime? RealDeliveryDate { get; set; }
    public string MerchandiseOrderCodes { get; init; } = string.Empty;
}

public sealed class PurchaseOrderDetailDto : PurchaseOrderListItemDto
{
    public string? SupplierContact { get; init; }
    public string? SupplierAddress { get; init; }
    public string? SupplierPhone { get; init; }
    public string? EmployeeFullName { get; init; }
    public string? EmployeePhone { get; init; }
    public string? PlpuComment { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentTypes { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public PurchaseOrderSnapshotDto? PurchaseOrderSnapshot { get; init; }
    public string MerchadiseOrderList => MerchandiseOrderCodes;
    public IReadOnlyList<PurchaseOrderMerchandiseOrderDto> MerchandiseOrders { get; init; } = [];
    public IReadOnlyList<PurchaseOrderLineDto> PurchaseOrderDetails => Items;
    public IReadOnlyList<PurchaseOrderLineDto> Items { get; init; } = [];
}

public sealed class PurchaseOrderMerchandiseOrderDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
}

public sealed class PurchaseOrderLineDto
{
    public Guid PurchaseOrderDetailId { get; init; }
    public int LineNo { get; init; }
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal RequestQuantity => Quantity;
    public string MaterialExternalIDSnapshot => MaterialCode;
    public string MaterialNameSnapshot => MaterialName;
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

public sealed class PurchaseOrderSnapshotDto
{
    public string? EmployeeExternalIdSnapshot { get; init; }
    public string? EmployeeFullNameSnapshot { get; init; }
    public string? PhoneNumberSnapshot { get; init; }
    public string? EmailSnapshot { get; init; }
    public string? SupplierExternalIdSnapshot { get; init; }
    public string? SupplierNameSnapshot { get; init; }
    public string? SupplierContactSnapshot { get; init; }
    public string? SupplierPhoneNumberSnapshot { get; init; }
    public string? SupplierAddressSnapshot { get; init; }
    public decimal? TotalPrice { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentTypes { get; init; }
    public int? Vat { get; init; }
}
