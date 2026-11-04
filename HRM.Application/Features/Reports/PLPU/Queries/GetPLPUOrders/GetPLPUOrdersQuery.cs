using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrders;
public sealed class GetPLPUOrdersQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUPagedResult<PLPUPurchaseOrderRowDto>>>
{
}
