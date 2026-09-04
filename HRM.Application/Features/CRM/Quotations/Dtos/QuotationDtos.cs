using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class CreateQuotationRequest
{
    public string? ExternalId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? ContactId { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? CustomerAddressSnapshot { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal ExchangeRate { get; init; } = 1m;
    public decimal TaxPercent { get; init; }
    public DateTime? QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<QuotationTermRequest> Terms { get; init; } = [];
    public IReadOnlyList<QuotationLineRequest> Lines { get; init; } = [];
}

public sealed class UpdateQuotationRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ContactId { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? CustomerAddressSnapshot { get; init; }
    public string? Currency { get; init; }
    public decimal? ExchangeRate { get; init; }
    public decimal? TaxPercent { get; init; }
    public DateTime? QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? Note { get; init; }
    /// <summary>Không gửi là giữ nguyên; gửi danh sách rỗng là ngừng áp dụng toàn bộ điều khoản tùy chỉnh.</summary>
    public IReadOnlyList<QuotationTermRequest>? Terms { get; init; }
}

public sealed class QuotationTermRequest
{
    public string LabelVi { get; init; } = string.Empty;
    public string? LabelEn { get; init; }
    public string? ValueVi { get; init; }
    public string? ValueEn { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class ReplaceQuotationLinesRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public IReadOnlyList<QuotationLineRequest> Lines { get; init; } = [];
}

public sealed class QuotationLineRequest
{
    public Guid ProductId { get; init; }
    public Guid? SampleRequestId { get; init; }
    public Guid? ProductPricingVersionId { get; init; }
    public decimal Quantity { get; init; }
    public string? Unit { get; init; }
    public QuotationLinePriceMode PriceMode { get; init; } = QuotationLinePriceMode.FormulaCalculatedLocked;
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public IReadOnlyList<QuotationLinePriceTierRequest> PriceTiers { get; init; } = [];
    public string? Note { get; init; }
    public int? SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class QuotationLinePriceTierRequest
{
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; } = true;
    public bool MaxInclusive { get; init; } = true;
    public decimal UnitPrice { get; init; }
    public decimal CommissionAmount { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class RefreshQuotationPricesRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }
    public IReadOnlyList<QuotationPriceRequest> Lines { get; init; } = [];
}

public sealed class QuotationPriceRequest
{
    public Guid QuotationLineId { get; init; }
    public Guid ProductPricingVersionId { get; init; }
}

public sealed class MarkQuotationSentRequest
{
    public DateTime? ExpectedUpdatedDate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerInteractionType InteractionType { get; init; } = CustomerInteractionType.Quotation;
    public string? Note { get; init; }
}

public sealed class QuotationCreateResultDto
{
    public Guid QuotationId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed class QuotationTotalsDto
{
    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

public sealed class QuotationListItemDto
{
    public Guid QuotationId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public QuotationStatus Status { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public DateTime? SentDate { get; init; }
    public int LineCount { get; init; }
}

public sealed class QuotationDetailDto
{
    public Guid QuotationId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid? ContactId { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? CustomerAddressSnapshot { get; init; }
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public QuotationStatus Status { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal ExchangeRate { get; init; }
    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime QuotationDate { get; init; }
    public DateTime? ValidUntil { get; init; }
    public DateTime? SentDate { get; init; }
    public string? PaymentTerms { get; init; }
    public string? DeliveryTerms { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<QuotationTermDto> Terms { get; init; } = [];
    public int Version { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<QuotationLineDto> Lines { get; init; } = [];
    public IReadOnlyList<QuotationStatusHistoryDto> StatusHistories { get; init; } = [];
}

public sealed class QuotationLineDto
{
    public Guid QuotationLineId { get; init; }
    public Guid ProductId { get; init; }
    public Guid? SampleRequestId { get; init; }
    public Guid? ProductPricingVersionId { get; init; }
    public int? ProductPricingVersion { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingStatus? ProductPricingStatus { get; init; }

    public bool HasApprovedPricingAvailable { get; init; }
    public bool HasNewerPricingVersion { get; init; }

    public string ProductExternalId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public QuotationLinePriceMode PriceMode { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal LineTotal { get; init; }
    /// <summary>
    /// Giá theo bậc của chính báo giá này. Khi <see cref="QuotationLinePriceTierDto.IsSnapshot"/>
    /// là false, các bậc chỉ là gợi ý để Sale nhập trước khi lưu.
    /// </summary>
    public IReadOnlyList<QuotationLinePriceTierDto> PriceTiers { get; set; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuotationDefaultPriceTierSource? DefaultPriceTierSource { get; set; }

    /// <summary>Giá chuẩn President đã duyệt; không có thì null.</summary>
    public QuotationApprovedTierPricingDto? ApprovedPricing { get; set; }

    /// <summary>Giá hệ thống tính theo công thức/NVL và policy hiện hành; không lưu vào báo giá.</summary>
    public QuotationSystemCalculatedTierPricingDto? SystemCalculatedPricing { get; set; }

    /// <summary>Giá từ báo giá Sent gần nhất của cùng khách hàng và sản phẩm; không có thì null.</summary>
    public QuotationLatestQuotedTierPricingDto? LatestQuotedPricing { get; set; }

    public string? Note { get; init; }
    public int SortOrder { get; init; }
}

public sealed class QuotationLinePriceTierDto
{
    public Guid QuotationLinePriceTierId { get; init; }
    public bool IsSnapshot { get; init; }
    /// <summary>Tier đã lưu có đang được áp dụng hay chỉ được giữ lại để xem/chỉnh sửa.</summary>
    public bool IsActive { get; init; }
    public bool RequiresManualPrice { get; init; }
    public string QuantityRangeLabel { get; init; } = string.Empty;
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
    public bool MinInclusive { get; init; }
    public bool MaxInclusive { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal CustomerUnitPrice { get; init; }
    public int SortOrder { get; init; }
}

public sealed class QuotationTermDto
{
    public Guid QuotationTermId { get; init; }
    public string LabelVi { get; init; } = string.Empty;
    public string? LabelEn { get; init; }
    public string? ValueVi { get; init; }
    public string? ValueEn { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class QuotationStatusHistoryDto
{
    public Guid Id { get; init; }
    public QuotationStatus FromStatus { get; init; }
    public QuotationStatus ToStatus { get; init; }
    public string? Note { get; init; }
    public Guid ChangedBy { get; init; }
    public string ChangedByName { get; init; } = string.Empty;
    public DateTime ChangedDate { get; init; }
}
