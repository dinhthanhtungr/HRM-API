using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.FinalDecision;

public sealed record FinalComplaintDecisionCommand(
    Guid ComplaintReportId,
    FinalComplaintDecisionRequest Request) : IRequest<OperationResult>;
