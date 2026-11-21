using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Reports.CompanyOverview;

public sealed class GetCompanyOverviewQuery : IRequest<OperationResult<CompanyOverviewDto>>
{
    public Guid? CompanyId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
