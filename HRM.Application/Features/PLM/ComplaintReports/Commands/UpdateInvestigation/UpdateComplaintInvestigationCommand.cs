using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateInvestigation;

public sealed record UpdateComplaintInvestigationCommand(
    Guid ComplaintReportId,
    UpdateComplaintInvestigationRequest Request) : IRequest<OperationResult>;
