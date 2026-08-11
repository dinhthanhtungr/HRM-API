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
    public int? TrialNo { get; set; }
    public bool HasTrial { get; set; }
    public string SampleRequestStatus { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string SampleRequestExternalId { get; set; } = string.Empty;
    public double? RequestedSampleQuantity { get; set; }
    public decimal? DeliveredSampleQuantityKg { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? ColourCode { get; set; }
    public string? BatchNo { get; set; }

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

    public decimal? AdditiveRate { get; set; }
    public string? LabNote { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
