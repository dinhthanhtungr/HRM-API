using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUReceipts;
public sealed class GetPLPUReceiptsQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUPagedResult<PLPUPurchaseReceiptReportDto>>>
{
}
