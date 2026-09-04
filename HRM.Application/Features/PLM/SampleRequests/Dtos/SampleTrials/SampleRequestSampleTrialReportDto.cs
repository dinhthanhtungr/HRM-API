using HRM.Domain.Enums.SampleRequests;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;

/// <summary>
/// Một dòng báo cáo cho một lần Lab hoàn thành hoặc gửi mẫu của Sample Request.
/// </summary>
public sealed class SampleRequestSampleTrialReportDto
{
    public Guid? SampleRequestSampleTrialId { get; set; }
    public Guid SampleRequestId { get; set; }
    public Guid? FormulaId { get; set; }
    public string? FormulaExternalId { get; set; }
    public int? TrialNo { get; set; }
    public bool HasTrial { get; set; }
    public int TrialCount { get; set; }
    public bool HasPreviousTrials { get; set; }
    public bool CanCreateTrial { get; set; }
    public bool CanUpdateTrial { get; set; }
    public bool CanUpdateCustomerFeedback { get; set; }
    public string SampleRequestStatus { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
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

    /// <summary>Thời điểm President/Developer duyệt version giá chuẩn.</summary>
    public DateTime? StandardSellingPriceApprovedAt { get; set; }

    /// <summary>Giá bán tiêu chuẩn realtime do pricing policy hiện hành tính.</summary>
    public decimal? SystemCalculatedStandardSellingPrice { get; set; }

    /// <summary>Ngày Sale yêu cầu có mẫu, lấy từ Sample Request.</summary>
    public DateTime? RequestDeliveryDate { get; set; }
    /// <summary>Ngày dự kiến có mẫu, lấy từ Sample Request.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }
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

    public double? AdditiveRate { get; set; }
    public string? LabNote { get; set; }
    /// <summary>Ngày tạo Sample Request; đây là mốc sắp xếp của danh sách chính.</summary>
    public DateTime SampleRequestCreatedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
