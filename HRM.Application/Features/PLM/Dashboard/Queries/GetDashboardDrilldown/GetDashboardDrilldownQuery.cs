using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Dashboard.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardDrilldown;

public sealed class GetDashboardDrilldownQuery
    : PaginationQuery, IRequest<PagedResult<PlmDashboardDrilldownItemDto>>
{
    public string Source { get; set; } = "sampleRequests";
    public string Completion { get; set; } = "all";
    public string? MonthKey { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }

    public string NormalizedSource => string.IsNullOrWhiteSpace(Source)
        ? "sampleRequests"
        : Source.Trim();

    public string NormalizedCompletion => string.IsNullOrWhiteSpace(Completion)
        ? "all"
        : Completion.Trim();
}
