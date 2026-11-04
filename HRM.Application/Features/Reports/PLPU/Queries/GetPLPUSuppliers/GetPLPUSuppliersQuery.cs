using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Reports.PLPU.Queries.GetPLPUSuppliers;
public sealed class GetPLPUSuppliersQuery : PLPUPurchaseOverviewQuery, IRequest<OperationResult<List<PLPUSupplierPurchasePerformanceDto>>>
{
}
