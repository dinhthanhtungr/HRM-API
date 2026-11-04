using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUFinish;
public sealed class GetPLPUFinishQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<List<FinishRow>>>
{
}
