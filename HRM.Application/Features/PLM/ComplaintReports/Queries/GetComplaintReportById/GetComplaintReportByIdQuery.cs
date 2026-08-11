using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.GetComplaintReportById;

public sealed record GetComplaintReportByIdQuery(Guid ComplaintReportId) : IRequest<ComplaintReportDetailDto?>;
