using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUQc;
public sealed class GetPLPUQcQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUPagedResult<PLPUPurchaseQcReportDto>>>
{
}
