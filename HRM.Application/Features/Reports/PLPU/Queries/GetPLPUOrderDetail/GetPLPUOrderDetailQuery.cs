using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrderDetail;
public sealed class GetPLPUOrderDetailQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<PLPUPurchaseOrderDetailDashboardDto>>
{
    public Guid Id { get; set; }
}
