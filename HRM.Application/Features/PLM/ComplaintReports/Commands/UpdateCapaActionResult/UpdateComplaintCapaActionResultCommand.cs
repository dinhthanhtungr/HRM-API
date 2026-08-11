using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.UpdateCapaActionResult;

public sealed record UpdateComplaintCapaActionResultCommand(
    Guid ComplaintReportId,
    Guid ActionId,
    UpdateComplaintCapaActionResultRequest Request) : IRequest<OperationResult>;
