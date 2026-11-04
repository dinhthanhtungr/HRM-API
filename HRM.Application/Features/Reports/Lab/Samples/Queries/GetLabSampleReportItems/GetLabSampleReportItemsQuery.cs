using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.Reports.Lab.Samples.Queries.GetLabSampleReportItems;

public sealed class GetLabSampleReportItemsQuery : LabSampleReportFilter, IRequest<OperationResult<PagedResult<LabSampleRowDto>>>
{
    public LabSampleMetric Metric { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
