using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Queries.ExportPdf;

public sealed record ExportComplaintReportPdfQuery(Guid ComplaintReportId)
    : IRequest<OperationResult<ComplaintReportPdfFileDto>>;
