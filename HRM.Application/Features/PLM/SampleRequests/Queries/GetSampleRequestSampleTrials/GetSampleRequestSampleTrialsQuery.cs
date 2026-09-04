using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

public enum SampleTrialReportType
{
    All,
    CompletedSamples,
    WaitingCustomerFeedback
}

/// <summary>
/// Lấy báo cáo phân trang theo từng lần gửi/thử mẫu để FE trình bày dạng bảng.
/// </summary>
public sealed class GetSampleRequestSampleTrialsQuery
    : PaginationQuery, IRequest<PagedResult<SampleRequestSampleTrialReportDto>>
{
    public const string DefaultCurrency = "VND";

    public Guid? SampleRequestId { get; set; }
    public Guid? CustomerId { get; init; }
    /// <summary>
    /// Inclusive report-date lower bound. CompletedSamples uses CustomerReplyDate,
    /// WaitingCustomerFeedback uses RequestReceivedDate, and All uses SampleRequest.CreatedDate.
    /// </summary>
    public DateTime? FromDate { get; init; }
    /// <summary>
    /// Inclusive report-date upper bound with the same report-specific date semantics as FromDate.
    /// </summary>
    public DateTime? ToDate { get; init; }
    /// <summary>
    /// Inclusive calendar-date cutoff applied to SampleRequest.CreatedDate.
    /// This filter is independent from the report-specific FromDate/ToDate semantics.
    /// </summary>
    public DateOnly? SampleRequestCreatedToDate { get; init; }
    /// <summary>
    /// When false and SampleRequestCreatedToDate is provided, only Sample Requests
    /// created in the cutoff month are returned. Defaults to true for compatibility.
    /// </summary>
    public bool IncludePreviousUnfinished { get; init; } = true;
    public SampleTrialReportType? ReportType { get; init; }
    public SampleTrialStatus? Status { get; init; }
    public string? CustomerReplyStatus { get; init; }

    [StringLength(10)]
    public string? Currency { get; init; } = DefaultCurrency;

    [JsonIgnore]
    public string NormalizedCurrency => string.IsNullOrWhiteSpace(Currency)
        ? DefaultCurrency
        : Currency.Trim().ToUpperInvariant();
    /// <summary>
    /// Chỉ dùng cho route lịch sử của một Sample Request. Danh sách chính luôn trả Trial mới nhất.
    /// </summary>
    public bool IncludeTrialHistory { get; set; }
}
