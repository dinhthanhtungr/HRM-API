using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.Resubmit;

public sealed record ResubmitComplaintReportCommand(Guid ComplaintReportId) : IRequest<OperationResult>;
