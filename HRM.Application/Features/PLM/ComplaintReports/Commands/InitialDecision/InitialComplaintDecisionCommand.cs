using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.InitialDecision;

public sealed record InitialComplaintDecisionCommand(
    Guid ComplaintReportId,
    InitialComplaintDecisionRequest Request) : IRequest<OperationResult<ComplaintReportResultDto>>;
