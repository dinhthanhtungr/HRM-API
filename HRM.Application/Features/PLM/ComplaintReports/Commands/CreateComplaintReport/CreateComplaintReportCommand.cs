using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ComplaintReports.Commands.CreateComplaintReport;

public sealed record CreateComplaintReportCommand(
    CreateComplaintReportRequest Request,
    IReadOnlyList<AttachmentUploadFile> Files)
    : IRequest<OperationResult<ComplaintReportResultDto>>;
