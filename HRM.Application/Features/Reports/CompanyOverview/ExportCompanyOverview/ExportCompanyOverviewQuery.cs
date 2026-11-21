using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.CompanyOverview.ExportCompanyOverview;

public sealed class ExportCompanyOverviewQuery : IRequest<OperationResult<CompanyOverviewExportFile>>
{
    public Guid? CompanyId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
