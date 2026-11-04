using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUHeader;
public sealed class GetPLPUHeaderQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUPurchaseOverviewSummaryDto>>
{
}
