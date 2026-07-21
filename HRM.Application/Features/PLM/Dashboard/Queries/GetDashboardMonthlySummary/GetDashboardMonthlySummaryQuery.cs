using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Dashboard.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardMonthlySummary
{
    public sealed class GetDashboardMonthlySummaryQuery
        : PaginationQuery, IRequest<PagedResult<PlmMonthlySummaryDto>>
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public Guid? CompanyId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? CustomerId { get; set; }
        public string? Status { get; set; }
    }
}
