using HRM.Domain.Enums.SampleRequests;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

/// <summary>
/// Một dòng báo cáo cho một lần Lab hoàn thành hoặc gửi mẫu của Sample Request.
/// </summary>
public sealed class SampleRequestSampleTrialReportDto
{
    internal Services.SampleWorkDecision? WorkDecision { get; set; }
    [JsonIgnore]
    internal bool? CanActForSaleOwner { get; set; }
    public bool NeedsLabSchedule { get; set; }
    public bool HasAvailableFormula { get; set; }
    public bool CanEditLabSchedule { get; set; }
    public string? PrimaryActionCode { get; set; }
    public string[] SupplementaryActionCodes { get; set; } = [];
    public Reports.SampleFlowStateDto? Flow { get; set; }
    public SampleFeedbackSummaryDto[] FeedbackSummaries { get; set; } = [];
    public string[] LabTeamCodes { get; set; } = [];
    [JsonIgnore]
    public string? LabCategoryCode { get; set; }
    public bool CanRecordCustomerFeedbackNote { get; set; }
    public Guid? LatestPriceEventId { get; set; }
    public bool CanCorrectCustomerFeedback { get; set; }
    public bool IsLegacyWork { get; set; }
    public bool IsLegacyWorkTracked { get; set; }
    public bool CanManageLegacyWorkTracking { get; set; }
    [JsonIgnore]
    public Guid ProductId { get; set; }
    [JsonIgnore]
    public Guid ManagerSalesEmployeeId { get; set; }
    public string? InfoType { get; set; }
    public string? FormulaStatus { get; set; }
    public DateTime? RealPriceQuoteDate { get; set; }
    public Guid? SampleRequestSampleTrialId { get; set; }
    public Guid SampleRequestId { get; set; }
    public Guid? FormulaId { get; set; }
    /// <summary>Internal price-confirmation basis, independent of the displayed historical Trial formula.</summary>
    [JsonIgnore]
    public Guid? PriceConfirmationFormulaId { get; set; }
    public string? FormulaExternalId { get; set; }
    public int? TrialNo { get; set; }
    public bool HasTrial { get; set; }
    public int TrialCount { get; set; }
    public bool HasPreviousTrials { get; set; }
    public bool CanCreateTrial { get; set; }
    public bool CanUpdateTrial { get; set; }
    public bool CanUpdateExpectedPriceQuoteDate { get; set; }
    public bool CanUpdateCustomerFeedback { get; set; }
    public string SampleRequestStatus { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerExternalId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public bool IsCustomerPriceContentHidden { get; set; }
    public CustomerPriceConfirmationDto? CustomerPriceConfirmation { get; set; }
    public bool RequiresCustomerPriceConfirmation { get; set; }
    public DateTime? CustomerPriceConfirmedAt { get; set; }
    public Guid? CustomerPriceConfirmedByEmployeeId { get; set; }
    public string? CustomerPriceConfirmedByName { get; set; }
    public Guid? CustomerPriceQuotationLineId { get; set; }
    public string? CustomerPriceReference { get; set; }
    public string? CustomerPriceConfirmationNote { get; set; }
    public bool CanConfirmCustomerPrice { get; set; }
    public bool CanRevokeCustomerPrice { get; set; }
    public bool CanReceiveLabStandard { get; set; }
    public DateTime? SaleFollowUpDate { get; set; }
    public string ManagerSalesName { get; set; } = string.Empty;
    public string SampleRequestExternalId { get; set; } = string.Empty;
    public double? RequestedSampleQuantity { get; set; }
    public decimal? DeliveredSampleQuantityKg { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? ColourCode { get; set; }
    public string? BatchNo { get; set; }

    /// <summary>Giá bán tiêu chuẩn của ProductPricingVersion Approved mới nhất.</summary>
    public decimal? ApprovedStandardSellingPrice { get; set; }

    /// <summary>Ghi chú công khai đi cùng ProductPricingVersion Approved mới nhất.</summary>
    public string? PublisherNote { get; set; }

    /// <summary>Thời điểm President/Developer duyệt version giá chuẩn.</summary>
    public DateTime? StandardSellingPriceApprovedAt { get; set; }

    /// <summary>
    /// Giá bán tiêu chuẩn realtime do pricing policy hiện hành tính; trả null khi user không có
    /// capability xem giá hệ thống tính (bao gồm SaleUser).
    /// </summary>
    public decimal? SystemCalculatedStandardSellingPrice { get; set; }

    /// <summary>Ngày Sale yêu cầu có mẫu, lấy từ Sample Request.</summary>
    public DateTime? RequestDeliveryDate { get; set; }
    /// <summary>Ngày Lab thực tế nhận mẫu đầu vào từ Sale, lấy từ Sample Request.</summary>
    public DateTime? LabReceivedDate { get; set; }
    public bool IsDelayed { get; set; }
    public int? NumberDeliverySampleDate { get; set; }
    public Guid? FormulaCreatedByEmployeeId { get; set; }
    public string? FormulaCreatedByName { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? FormulaCreatorLeaderNames { get; set; }
    /// <summary>Ngày Lab xác nhận công thức đúng hồ sơ; không phải ngày gửi mẫu.</summary>
    public DateTime? LabFormulaConfirmedAt { get; set; }
    public DateTime? SampleRequestUpdatedDate { get; set; }
    public bool CanUpdateLabProgress { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsLabWorkComplete { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LabCompletedAt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DueDateSource { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DelayReason { get; set; }
    public Guid? LabReceivedByEmployeeId { get; set; }
    public string? LabReceivedByName { get; set; }
    /// <summary>Ngày dự kiến có mẫu, lấy từ Sample Request.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? ExpectedPriceQuoteDate { get; set; }
    public DateTime? RequestReceivedDate { get; set; }
    public DateTime? FinishedDate { get; set; }
    public DateTime? SentDate { get; set; }
    public int? TurnaroundDays { get; set; }

    public Guid? SentByEmployeeId { get; set; }
    public string? SentByName { get; set; }
    public string? DeliveryMethod { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleTrialStatus? Status { get; set; }
    public string? CustomerReplyStatus { get; set; }
    public DateTime? CustomerReplyDate { get; set; }
    public string? CustomerReplyNote { get; set; }
    public DateTime? OrderDate { get; set; }

    /// <summary>Tỷ lệ sử dụng hiện tại từ SampleRequest.Product.UsageRate; không phải snapshot của Trial.</summary>
    public double? AdditiveRate { get; set; }
    public string? LabNote { get; set; }
    /// <summary>Ngày tạo Sample Request; đây là mốc sắp xếp của danh sách chính.</summary>
    public DateTime SampleRequestCreatedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkStatus { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextActionCode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextActionLabel { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponsibleRole { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ResponsibleEmployeeId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsActionForCurrentUser { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? DueDate { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsOverdue { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FeedbackRecordedOnDate { get; set; }
}
