using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateEffectiveness;

public sealed record UpdateComplaintEffectivenessCommand(
    Guid ComplaintReportId,
    UpdateComplaintEffectivenessRequest Request) : IRequest<OperationResult>;
