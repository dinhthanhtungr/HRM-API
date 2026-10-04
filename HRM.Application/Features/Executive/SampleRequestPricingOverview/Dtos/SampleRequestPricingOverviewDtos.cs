using System.Text.Json.Serialization;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Application.Features.Executive.MerchandiseOrderPriceHistory.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;

namespace HRM.Application.Features.Executive.SampleRequestPricingOverview.Dtos;

public sealed class SampleRequestPricingOverviewItemDto
{
    public Guid SampleRequestId { get; init; }
    public string RequestCode { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime LatestActivityAt { get; init; }
    public SampleRequestPricingProductDto Product { get; init; } = new();
    public SampleRequestPricingCustomerDto Customer { get; init; } = new();
    public SampleRequestPricingDeliveryDto Delivery { get; init; } = new();
    public SampleRequestPricingDto Pricing { get; init; } = new();
    public SampleRequestConversationOverviewDto Conversation { get; init; } = new();
    public SampleRequestPricingActionsDto Actions { get; init; } = new();
    public LatestMerchandiseOrderDto? LatestMerchandiseOrder { get; init; }
}

public sealed class SampleRequestPricingProductDto
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ColourCode { get; init; }
    public string? ColorValue { get; init; }
    public string? ColorDisplayName { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryCode { get; init; }
    public string? CategoryName { get; init; }
    public string? AdditiveCode { get; init; }
    public string? AdditiveDisplayName { get; init; }
    public Guid? LabEmployeeId { get; init; }
    public string? LabName { get; init; }
}

public sealed class SampleRequestPricingCustomerDto
{
    public Guid CustomerId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid? SaleEmployeeId { get; init; }
    public string? SaleName { get; init; }
}

public sealed class SampleRequestPricingDeliveryDto
{
    public DateTime? RequestedDate { get; init; }
    public DateTime? ExpectedDate { get; init; }
}

public sealed class SampleRequestPricingDto
{
    public string Currency { get; init; } = string.Empty;
    public decimal? StandardSellingPrice { get; init; }
    public string? PublisherNote { get; init; }
    public decimal? CurrentMaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public decimal? StandardSellingPriceDifference { get; init; }
    public decimal? StandardSellingPriceDifferencePercent { get; init; }
    public bool HasRealtimePriceComparison { get; init; }
    public StandardPriceRealtimeComparisonDto? RealtimePriceComparison { get; init; }
    public IReadOnlyList<QuotationProductPricingMaterialDto> Materials { get; init; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingLookupStatus PricingStatus { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingHealthStatus PricingHealthStatus { get; init; }

    public bool RequiresPricingAction { get; init; }
    public DateTime? PricingReviewDueDate { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductStandardPriceState StandardPriceState { get; init; }
    public bool HasFormulaConfirmationPending { get; init; }
    public bool IsPricingReviewExpired { get; init; }
    public IReadOnlyList<ProductPricingAttentionSource> PricingAttentionSources { get; init; } = [];
    public DateTime? PricingUpdatedDate { get; init; }
    public int WaitingQuotationCount { get; init; }
    public Guid? DraftPricingVersionId { get; init; }
    public Guid? ApprovedPricingVersionId { get; init; }
    public SampleRequestDisplayedPricingFormulaDto? DisplayedFormula { get; init; }
    public SampleRequestRecommendedPricingFormulaDto? RecommendedFormula { get; init; }
}

/// <summary>
/// Công thức/source thực tế tạo ra giá đang hiển thị. Khi có pricing version,
/// đây là source đã gắn với giá chuẩn; nếu chưa có version, đây là source được
/// Workbench chọn để tính giá hệ thống.
/// </summary>
public sealed class SampleRequestDisplayedPricingFormulaDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Status { get; init; }
    public string PriceKind { get; init; } = string.Empty;
}

/// <summary>
/// Công thức hệ thống đề xuất sử dụng theo mốc nghiệp vụ mới nhất. Đây có thể khác
/// DisplayedFormula, là source đang gắn với giá chuẩn/Draft hiện hành.
/// </summary>
public sealed class SampleRequestRecommendedPricingFormulaDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PricingReviewSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsEligible { get; init; }
    public bool IsCurrentlyApplied { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime CandidateAt { get; init; }
    public string CandidateDateSource { get; init; } = string.Empty;
    public string Currency { get; init; } = "VND";
    public decimal? RealtimeMaterialCost { get; init; }
    public decimal? ManufacturingCost { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public decimal? RealtimeStandardSellingPrice { get; init; }
    public string? PriceCalculationFormula { get; init; }
    public bool IsRealtimeMaterialCostComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }
    public DateTime CalculatedAt { get; init; }
}

public sealed class SampleRequestConversationOverviewDto
{
    public Guid? ConversationId { get; init; }
    public int TotalMessageCount { get; init; }
    public int UnreadCount { get; init; }
    public DateTime? LastMessageAt { get; init; }
}

public sealed class SampleRequestPricingActionsDto
{
    public bool CanOpenSampleRequest { get; init; }
    public bool CanOpenPricingDetail { get; init; }
    public bool CanManagePricing { get; init; }
    public bool CanOpenConversation { get; init; }
}
