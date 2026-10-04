using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

public enum SampleRequestDailyView
{
    Today,
    All
}

/// <summary>Những yêu cầu cần xử lý hoặc theo dõi vào một ngày, theo quyền xem Sample Request hiện hành.</summary>
public sealed class GetSampleRequestDailyWorkQuery
    : PaginationQuery, IRequest<PagedResult<SampleRequestSampleTrialReportDto>>
{
    public DateOnly? Date { get; init; }
    public SampleRequestDailyView View { get; init; } = SampleRequestDailyView.Today;
    public Guid? CustomerId { get; init; }
    public Guid? SaleEmployeeId { get; init; }
    public Guid? SampleRequestId { get; init; }
    [StringLength(10)]
    public string? Currency { get; init; } = "VND";
}
