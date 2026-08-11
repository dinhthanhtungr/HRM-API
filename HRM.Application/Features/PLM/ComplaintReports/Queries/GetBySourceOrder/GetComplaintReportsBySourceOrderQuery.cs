using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetBySourceOrder;

public sealed record GetComplaintReportsBySourceOrderQuery(Guid MerchandiseOrderId)
    : IRequest<IReadOnlyList<ComplaintReportListItemDto>>;
