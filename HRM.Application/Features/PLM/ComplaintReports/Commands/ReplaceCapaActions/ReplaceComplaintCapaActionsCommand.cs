using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.ReplaceCapaActions;

public sealed record ReplaceComplaintCapaActionsCommand(
    Guid ComplaintReportId,
    ReplaceComplaintCapaActionsRequest Request) : IRequest<OperationResult>;
