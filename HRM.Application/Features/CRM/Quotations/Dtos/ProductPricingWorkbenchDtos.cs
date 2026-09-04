using System.Text.Json.Serialization;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public enum ProductPricingWorkbenchView
{
    NeedsPricing = 0,
    Draft = 10,
    Approved = 20,
    All = 30
}

public sealed class ProductPricingWorkbenchItemDto
{
    public bool CanOpenPricingDetail { get; init; }
    public bool CanManagePricing { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingLookupStatus PricingStatus { get; init; }
    public bool IsSystemCalculatedDraft { get; init; }

    /// <summary>
    /// Khả năng dùng dữ liệu định giá hiện tại. Khác với SourceIsEligible là trạng thái kỹ thuật của Formula.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingHealthStatus PricingHealthStatus { get; init; }
    public bool RequiresPricingAction { get; init; }
    public DateTime? PricingReviewDueDate { get; init; }

    public int WaitingQuotationCount { get; init; }
    public DateTime? LatestRequestedAt { get; init; }
    /// <summary>
    /// Toàn bộ khách hàng liên quan qua Sample Request hoặc quotation active của sản phẩm này. Chỉ President/Developer nhận được
    /// customer health summary để đánh giá ngữ cảnh trước khi duyệt giá.
    /// </summary>
    public IReadOnlyList<ProductPricingWorkbenchCustomerContextDto> RelatedCustomers { get; init; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductPricingSourceType? SourceType { get; init; }

    public Guid? SourceId { get; init; }
    public string? SourceExternalId { get; init; }
    public string? SourceName { get; init; }
    public string? SourceStatus { get; init; }
    public bool SourceIsEligible { get; init; }
    public bool SourceIsCustomerSelected { get; init; }

    public decimal? CurrentMaterialCost { get; init; }
    public bool IsCurrentMaterialCostComplete { get; init; }
    public int MissingMaterialPriceCount { get; init; }
    public decimal? StoredMaterialCostSnapshot { get; init; }
    public decimal? MaterialCostDifference { get; init; }
    public decimal? MaterialCostDifferencePercent { get; init; }

    public decimal? ManufacturingCost { get; init; }
    public bool UsedDefaultManufacturingCost { get; init; }
    /// <summary>
    /// Giá bán tiêu chuẩn snapshot của version Draft, hoặc Approved khi chưa có Draft.
    /// Chỉ fallback sang giá realtime khi chưa có version giá nào được lưu.
    /// </summary>
    public decimal? StandardSellingPrice { get; init; }
    public decimal? RealtimeStandardSellingPrice { get; init; }
    public decimal? StandardSellingPriceDifference { get; init; }
    public decimal? StandardSellingPriceDifferencePercent { get; init; }
    public bool HasRealtimePriceComparison { get; init; }
    public decimal? ProfitMarginRate { get; init; }

    public Guid? DraftPricingVersionId { get; init; }
    public Guid? ApprovedPricingVersionId { get; init; }
    public DateTime? PricingUpdatedDate { get; init; }
    public DateTime? PriceConfirmedAt { get; init; }
    public DateTime? PriceExpiresAt { get; init; }
    public int? RemainingValidityDays { get; init; }
    public int? OverdueDays { get; init; }
    public bool? IsPriceExpired { get; init; }
}

/// <summary>
/// Ngữ cảnh khách hàng từ Sample Request hoặc quotation active của sản phẩm. AI summary là snapshot mới nhất đã tạo thành công;
/// null nghĩa là khách chưa có AI summary hợp lệ, không phải dữ liệu bị suy diễn.
/// </summary>
public sealed class ProductPricingWorkbenchCustomerContextDto
{
    public Guid CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public int RelatedDocumentCount { get; init; }
    public DateTime LatestRelatedDate { get; init; }
    public ProductPricingWorkbenchCustomerHealthSummaryDto? HealthSummary { get; init; }
}

/// <summary>
/// Phần thông tin AI Health cần thiết để người duyệt pricing hiểu nhu cầu, giai đoạn và rủi ro khách hàng.
/// </summary>
public sealed class ProductPricingWorkbenchCustomerHealthSummaryDto
{
    public string Summary { get; init; } = string.Empty;
    public string CustomerNeed { get; init; } = string.Empty;
    public string CurrentStage { get; init; } = string.Empty;
    public string Risk { get; init; } = string.Empty;
    public string NextAction { get; init; } = string.Empty;
    public string Sentiment { get; init; } = string.Empty;
    public DateTime? GeneratedAt { get; init; }
}

public sealed class ProductPricingWorkbenchDetailDto
{
    public ProductPricingWorkbenchItemDto Summary { get; init; } = new();
    public decimal? ManufacturingCost { get; init; }
    public decimal? StandardSellingPrice { get; init; }
    public decimal? ProfitMarginRate { get; init; }
    public ProductPricingSourceOptionDto? SelectedSource { get; init; }
    public ProductPricingVersionDto? DraftPricing { get; init; }
    public ProductPricingVersionDto? ApprovedPricing { get; init; }
    public IReadOnlyList<QuotationPricingWorkspaceTierDto> DisplayPriceTiers { get; init; } = [];
    public IReadOnlyList<ProductPricingVersionDto> PricingHistory { get; init; } = [];
    public IReadOnlyList<ProductPricingRelatedQuotationDto> RelatedQuotations { get; init; } = [];
}

public sealed class ProductPricingRelatedQuotationDto
{
    public Guid QuotationId { get; init; }
    public string QuotationExternalId { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerExternalId { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public Guid SaleEmployeeId { get; init; }
    public string SaleEmployeeName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}
