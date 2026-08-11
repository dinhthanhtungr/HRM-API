using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateReception;

public sealed record UpdateComplaintReceptionCommand(
    Guid ComplaintReportId,
    UpdateComplaintReceptionRequest Request)
    : IRequest<OperationResult<ComplaintReportResultDto>>;
