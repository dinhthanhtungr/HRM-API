using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUBody;
public sealed class GetPLPUBodyQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUPurchaseBodyDto>>
{
}
