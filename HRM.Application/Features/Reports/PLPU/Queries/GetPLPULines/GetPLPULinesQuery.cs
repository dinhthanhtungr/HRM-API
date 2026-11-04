using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPULines;
public sealed class GetPLPULinesQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUPagedResult<PLPUPurchaseOrderDetailReportDto>>>
{
}
