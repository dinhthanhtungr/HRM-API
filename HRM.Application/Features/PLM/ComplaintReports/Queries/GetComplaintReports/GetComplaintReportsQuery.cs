using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Domain.Enums.Orders;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetComplaintReports;

public sealed class GetComplaintReportsQuery
    : PaginationQuery, IRequest<PagedResult<ComplaintReportListItemDto>>
{
    public Guid? CustomerId { get; init; }
    public ComplaintReportStatus? Status { get; init; }
    public ComplaintResolutionType? ResolutionType { get; init; }
    public DateTime? ReportedFrom { get; init; }
    public DateTime? ReportedTo { get; init; }
    public bool AssignedToMe { get; init; }
    public Guid? SourceOrderId { get; init; }
}
