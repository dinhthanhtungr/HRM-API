using System.Text.Json.Serialization;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Features.PLM.SaleOrders.Dtos;

/// <summary>
/// Kết quả tạo SaleOrder, gồm định danh, collection đính kèm và trạng thái do backend quyết định.
/// </summary>
public sealed class CreateSaleOrderResultDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid AttachmentCollectionId { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Dữ liệu một dòng sản phẩm khi tạo SaleOrder; backend tính lại thành tiền từ số lượng và đơn giá.
/// </summary>
public sealed record SaleOrderLineRequest
{
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid ProductId { get; init; }
    public string? ProductExternalIdSnapshot { get; init; }
    public string? ProductNameSnapshot { get; init; }
    public Guid FormulaId { get; init; }
    public string? FormulaExternalIdSnapshot { get; init; }
    public decimal ExpectedQuantity { get; init; }
    public decimal? RealQuantity { get; init; }
    public string? BagType { get; init; }
    public string? PackageWeight { get; init; }
    public string? Comment { get; init; }
    public DateTime DeliveryRequestDate { get; init; }
    public DateTime? DeliveryActualDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public decimal BaseCostSnapshot { get; init; }
    public decimal RecommendedUnitPrice { get; init; }
    public decimal UnitPriceAgreed { get; init; }
}

/// <summary>
/// Request tạo SaleOrder gồm thông tin giao nhận và các dòng sản phẩm.
/// Company, mã đơn, audit và sale phụ trách khách hàng được backend tự resolve.
/// </summary>
public sealed record CreateSaleOrderRequest
{
    public Guid MerchandiseOrderId { get; init; }
    public Guid AttachmentCollectionId { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerNameSnapshot { get; init; }
    public string? CustomerExternalIdSnapshot { get; init; }
    public string? PhoneSnapshot { get; init; }
    public string? Receiver { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentType { get; init; }
    public decimal? Vat { get; init; }
    public string? Currency { get; init; }
    public decimal? ExchangeRate { get; init; }
    public bool IsPaid { get; init; }
    public DateTime? PaymentDate { get; init; }
    public string? Note { get; init; }
    public string? ShippingMethod { get; init; }
    public string? PONo { get; init; }
    public IReadOnlyCollection<SaleOrderLineRequest> SaleOrderDetails { get; init; } = Array.Empty<SaleOrderLineRequest>();
}

/// <summary>
/// Request patch phần header SaleOrder; field không gửi được giữ nguyên theo semantics của PatchHelper.
/// </summary>
public sealed record UpdateSaleOrderRequest
{
    public Guid MerchandiseOrderId { get; init; }
    public string? CustomerNameSnapshot { get; init; }
    public string? CustomerExternalIdSnapshot { get; init; }
    public string? PhoneSnapshot { get; init; }
    public string? Receiver { get; init; }
    public string? DeliveryAddress { get; init; }
    public decimal? Vat { get; init; }
    public string? PaymentType { get; init; }
    public DateTime? PaymentDate { get; init; }
    public string? Note { get; init; }
    public string? ShippingMethod { get; init; }
    public string? PONo { get; init; }
}

/// <summary>
/// Request tạm dừng hoặc mở lại giao hàng, kèm khoảng thời gian, lý do và loại tạm dừng.
/// </summary>
public sealed record PauseSaleOrderDeliveryRequest
{
    public Guid MerchandiseOrderId { get; init; }
    public bool? IsDeliveryPaused { get; init; }
    public DateTime? DeliveryPausedFrom { get; init; }
    public DateTime? DeliveryPausedTo { get; init; }
    public string? DeliveryPauseReason { get; init; }
    public string? DeliveryPauseType { get; init; }
}

/// <summary>
/// Request hủy SaleOrder; MerchandiseOrderId thực tế được controller gán từ route.
/// </summary>
public sealed record CancelSaleOrderRequest
{
    public Guid MerchandiseOrderId { get; init; }
    public string? DeletedReason { get; init; }
}

/// <summary>
/// Dữ liệu tóm tắt SaleOrder cho danh sách, bao gồm trạng thái pause giao hàng có hiệu lực.
/// </summary>
public class SaleOrderListItemDto
{
    public Guid MerchandiseOrderId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerNameSnapshot { get; init; } = string.Empty;
    public string CustomerExternalIdSnapshot { get; init; } = string.Empty;
    public Guid ManagerById { get; init; }
    public string ManagerByNameSnapshot { get; init; } = string.Empty;
    public decimal? TotalPrice { get; init; }
    public string? PaymentType { get; init; }
    public bool IsPaid { get; init; }
    public string Status { get; set; } = string.Empty;
    public string PONo { get; init; } = string.Empty;
    public DateTime CreateDate { get; init; }
    public Guid AttachmentCollectionId { get; init; }
    public bool IsDeliveryPaused { get; init; }
    public DateTime? DeliveryPausedFrom { get; init; }
    public DateTime? DeliveryPausedTo { get; init; }
    public string? DeliveryPauseReason { get; init; }
    public string? DeliveryPauseType { get; init; }
}

/// <summary>
/// Chi tiết SaleOrder gồm thông tin giao nhận, thanh toán và các dòng hàng active.
/// </summary>
public sealed class SaleOrderDetailDto : SaleOrderListItemDto
{
    public string PhoneSnapshot { get; init; } = string.Empty;
    public string Receiver { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;
    public decimal? Vat { get; init; }
    public string? Currency { get; init; }
    public decimal? ExchangeRate { get; init; }
    public DateTime? PaymentDate { get; init; }
    public string? Note { get; init; }
    public string? ShippingMethod { get; init; }
    public IReadOnlyCollection<SaleOrderLineDto> Lines { get; init; } = Array.Empty<SaleOrderLineDto>();
}

/// <summary>
/// Chi tiết một dòng SaleOrder, kèm số lượng đã giao và số lượng còn lại.
/// </summary>
public sealed class SaleOrderLineDto
{
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductExternalIdSnapshot { get; init; } = string.Empty;
    public string ProductNameSnapshot { get; init; } = string.Empty;
    public Guid FormulaId { get; init; }
    public string FormulaExternalIdSnapshot { get; init; } = string.Empty;
    public decimal ExpectedQuantity { get; init; }
    public decimal DeliveredQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
    public decimal? RealQuantity { get; init; }
    public string BagType { get; init; } = string.Empty;
    public string PackageWeight { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public DateTime DeliveryRequestDate { get; init; }
    public DateTime? DeliveryActualDate { get; init; }
    public DateTime? ExpectedDeliveryDate { get; init; }
    public decimal BaseCostSnapshot { get; init; }
    public decimal RecommendedUnitPrice { get; init; }
    public decimal UnitPriceAgreed { get; init; }
    public decimal TotalPriceAgreed { get; init; }
}

/// <summary>
/// Snapshot dòng bán gần nhất theo đúng khách hàng và sản phẩm, dùng để điền mặc định detail đơn mới.
/// </summary>
public sealed class SaleOrderProductDefaultsDto
{
    /// <summary>
    /// Cho biết có dữ liệu dòng SaleOrder cũ để điền mặc định hay không.
    /// Pricing hiện hành vẫn được trả ngay cả khi khách chưa có lịch sử mua.
    /// </summary>
    public bool HasPreviousSale { get; init; }
    public Guid MerchandiseOrderId { get; init; }
    public Guid MerchandiseOrderDetailId { get; init; }
    public Guid ProductId { get; init; }
    public Guid FormulaId { get; init; }
    public string BagType { get; init; } = string.Empty;
    public string PackageWeight { get; init; } = string.Empty;
    public decimal ExpectedQuantity { get; init; }
    public string FormulaExternalIdSnapshot { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public decimal UnitPriceAgreed { get; init; }
    public DateTime CreateDate { get; init; }
    public SaleOrderCurrentPricingDto CurrentPricing { get; init; } = new();
}

/// <summary>
/// Giá gợi ý hiện hành khi lập SaleOrder. Giá bán chuẩn đã duyệt được ưu tiên;
/// nếu chưa có, backend trả giá tính realtime từ pricing policy đang hiệu lực.
/// </summary>
public sealed class SaleOrderCurrentPricingDto
{
    public string Source { get; init; } = "Unavailable";
    public string Currency { get; init; } = "VND";
    public decimal? SuggestedUnitPrice { get; init; }

    public Guid? ProductPricingVersionId { get; init; }
    public int? ProductPricingVersion { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? CalculatedAt { get; init; }

    public Guid? FormulaPricingPolicyId { get; init; }
    public int? FormulaPricingPolicyVersion { get; init; }
    public DateTime? PricingPolicyEffectiveFrom { get; init; }

    public Guid? PricingSourceId { get; init; }
    public string PricingSourceType { get; init; } = string.Empty;
    public string PricingSourceExternalId { get; init; } = string.Empty;
    public string PricingStatus { get; init; } = "Unavailable";

    // Các khoản cost/margin chỉ có giá trị với FormulaPriceViewers.
    public decimal? MaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? CostBase { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public bool? IsMaterialCostComplete { get; init; }
    public int? MissingMaterialPriceCount { get; init; }

    /// <summary>
    /// Nguồn độc lập của PriceTiers. Nguồn này có thể khác Source của SuggestedUnitPrice.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SaleOrderPriceTierSource PriceTierSource { get; init; }

    public DateTime? PriceTierSourceDate { get; init; }
    public Guid? PriceTierQuotationId { get; init; }
    public string? PriceTierQuotationExternalId { get; init; }
    public IReadOnlyList<SaleOrderSuggestedPriceTierDto> PriceTiers { get; init; } = [];
}

public sealed class SaleOrderSuggestedPriceTierDto
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal? UnitPrice { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// Trạng thái pause giao hàng sau khi cập nhật thành công.
/// </summary>
public sealed class PauseSaleOrderDeliveryDto
{
    public Guid MerchandiseOrderId { get; init; }
    public bool IsDeliveryPaused { get; init; }
    public DateTime? DeliveryPausedFrom { get; init; }
    public DateTime? DeliveryPausedTo { get; init; }
    public string? DeliveryPauseReason { get; init; }
    public string? DeliveryPauseType { get; init; }
}
