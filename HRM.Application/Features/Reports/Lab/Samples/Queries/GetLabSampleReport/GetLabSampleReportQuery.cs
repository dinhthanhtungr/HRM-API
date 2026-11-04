using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.Lab.Samples.Queries.GetLabSampleReport;

public sealed class GetLabSampleReportQuery : LabSampleReportFilter, IRequest<OperationResult<LabSampleReportDto>>;
