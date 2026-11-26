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
public enum SampleRequestWorkSort { WorkPriority, CustomerCode, Newest }
public enum SampleRequestWorkBucket
{
    All, ActionRequired, WaitingLab, WaitingPrice, WaitingSale, FeedbackReceived, Rework, Completed, Cancelled,
    MyActionRequired, NewRequestRequired, LabProgress, Overdue, LabDeclined
}

/// <summary>Theo dõi mẫu theo phạm vi vận hành và nhóm công việc; hỗ trợ tra cứu legacy theo quyền.</summary>
public sealed class GetSampleRequestDailyWorkQuery
    : PaginationQuery, IRequest<PagedResult<SampleRequestSampleTrialReportDto>>
{
    internal bool WorkflowDetail { get; init; }
    internal bool SkipUnread { get; init; }
    internal bool ReadSelectedReportPage { get; init; }
    public DateOnly? Date { get; init; }
    /// <summary>Chỉ lấy phạm vi vận hành cố định, kể cả khi view=all dùng cho tìm kiếm trên báo cáo.</summary>
    public bool UseOperationalScope { get; init; }
    public SampleRequestDailyView View { get; init; } = SampleRequestDailyView.Today;
    public SampleRequestWorkAudience? Audience { get; init; }
    /// <summary>Chỉ đếm các nhóm từ một lượt đọc facts có scope, không dựng payload danh sách.</summary>
    public bool MenuCounts { get; init; }
    /// <summary>Page complete customer groups instead of individual requests. Pagination counts customers when enabled.</summary>
    public bool GroupByCustomer { get; init; }
    public HRM.Application.Features.PLM.SampleRequests.Services.SampleLabTeamFilter? LabTeam { get; init; }
    public SampleRequestWorkBucket Bucket { get; init; } = SampleRequestWorkBucket.All;
    /// <summary>Move these work categories first inside My work; never filter out other work.</summary>
    [MaxLength(13)]
    public string[]? PriorityTasks { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    [MaxLength(100)]
    public Guid[]? SaleEmployeeIds { get; init; }
    [MaxLength(100)]
    public Guid[]? SaleGroupIds { get; init; }
    public Guid? SampleRequestId { get; init; }
    [MaxLength(100)]
    public Guid[]? SampleRequestIds { get; init; }
    [StringLength(10)]
    public string? Currency { get; init; } = "VND";
}
