using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;

public enum SampleTrialReportType
{
    CompletedSamples,
    WaitingCustomerFeedback
}

/// <summary>
/// Lấy báo cáo phân trang theo từng lần gửi/thử mẫu để FE trình bày dạng bảng.
/// </summary>
public sealed class GetSampleRequestSampleTrialsQuery
    : PaginationQuery, IRequest<PagedResult<SampleRequestSampleTrialReportDto>>
{
    public Guid? SampleRequestId { get; init; }
    public Guid? CustomerId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public SampleTrialReportType? ReportType { get; init; }
    public SampleTrialStatus? Status { get; init; }
    public string? CustomerReplyStatus { get; init; }
}
