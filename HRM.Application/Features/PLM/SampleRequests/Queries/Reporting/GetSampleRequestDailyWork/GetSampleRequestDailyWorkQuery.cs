using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

public enum SampleRequestDailyView
{
    Today,
    All,
    Delay,
    SalePending,
    Tracking,
    SaleToday
}

public enum SampleRequestWorkAudience { Sale, Lab, LabAdmin }
public enum SampleRequestWorkBucket
{
    All, ActionRequired, WaitingLab, WaitingPrice, WaitingSale, FeedbackReceived, Rework, Completed, Cancelled,
    MyActionRequired, NewRequestRequired, LabProgress, Overdue
}

/// <summary>Theo dõi mẫu theo phạm vi vận hành và nhóm công việc; hỗ trợ tra cứu legacy theo quyền.</summary>
public sealed class GetSampleRequestDailyWorkQuery
    : PaginationQuery, IRequest<PagedResult<SampleRequestSampleTrialReportDto>>
{
    public DateOnly? Date { get; init; }
    /// <summary>Chỉ lấy phạm vi vận hành cố định, kể cả khi view=all dùng cho tìm kiếm trên báo cáo.</summary>
    public bool UseOperationalScope { get; init; }
    public SampleRequestDailyView View { get; init; } = SampleRequestDailyView.Today;
    public SampleRequestWorkAudience? Audience { get; init; }
    public HRM.Application.Features.PLM.SampleRequests.Services.SampleLabTeamFilter? LabTeam { get; init; }
    public SampleRequestWorkBucket Bucket { get; init; } = SampleRequestWorkBucket.All;
    public Guid? CustomerId { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    public Guid? SampleRequestId { get; init; }
    [StringLength(10)]
    public string? Currency { get; init; } = "VND";
}
