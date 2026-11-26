using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
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
    [JsonIgnore]
    internal Guid[]? SampleRequestIds { get; init; }
    internal bool ReadSelectedReportPage { get; init; }
    internal bool MenuCounts { get; init; }
    [JsonIgnore]
    public bool UseOperationalScope { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    [JsonIgnore]
    internal Guid[]? SaleEmployeeIds { get; init; }
    [JsonIgnore]
    internal Guid[]? SaleGroupIds { get; init; }
    /// <summary>
    /// Inclusive report-date lower bound. CompletedSamples uses CustomerReplyDate,
    /// WaitingCustomerFeedback uses SentDate, independent of legacy Sale receipt.
    /// All uses SampleRequest.CreatedDate.
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
    /// Chỉ dùng cho route lịch sử của một Sample Request. Danh sách chính trả Trial mới nhất,
    /// ngoại trừ WaitingCustomerFeedback trả mọi Trial đang chờ phản hồi.
    /// </summary>
    public bool IncludeTrialHistory { get; set; }
    internal bool UseTrialHistoryPayload => SampleRequestId.HasValue && IncludeTrialHistory &&
        WorkAudience == null && WorkSort == null && !GroupByCustomer && DailyWorkDate == null;
    [JsonIgnore]
    internal bool WorkflowDetail { get; init; }
    internal string[] PriorityTasks { get; init; } = [];

    /// <summary>Chỉ handler báo cáo ngày được đặt; route sample-trials không nhận filter nội bộ này từ client.</summary>
    [JsonIgnore]
    public DateOnly? DailyWorkDate { get; set; }

    [JsonIgnore]
    public SampleRequestDailyView DailyWorkView { get; set; } = SampleRequestDailyView.Today;
    [JsonIgnore]
    public SampleRequestWorkAudience? WorkAudience { get; set; }
    /// <summary>Internal daily-work option; counts/pages customers and includes every matching request in the selected groups.</summary>
    [JsonIgnore]
    internal bool GroupByCustomer { get; init; }
    [JsonIgnore]
    internal SampleRequestWorkSort? WorkSort { get; init; }
    [JsonIgnore]
    public HRM.Application.Features.PLM.SampleRequests.Services.SampleLabTeamFilter? LabTeam { get; set; }
    [JsonIgnore]
    public SampleRequestWorkBucket WorkBucket { get; set; }
    [JsonIgnore]
    public DateOnly? AdminPeriodFrom { get; set; }
    [JsonIgnore]
    public DateOnly? AdminPeriodTo { get; set; }
    [JsonIgnore]
    public Guid? AdminBranchId { get; set; }
    [JsonIgnore]
    public HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary.SampleRequestAdminMetric? AdminMetric { get; set; }
}
