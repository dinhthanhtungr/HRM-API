using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.RequestVerification;

public sealed record RequestComplaintVerificationCommand(Guid ComplaintReportId) : IRequest<OperationResult>;
