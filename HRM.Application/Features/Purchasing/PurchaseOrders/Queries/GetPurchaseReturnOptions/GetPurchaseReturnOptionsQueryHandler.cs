using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseReturnOptions;
internal sealed class GetPurchaseReturnOptionsQueryHandler(PurchaseOrderReturnService service)
    : IRequestHandler<GetPurchaseReturnOptionsQuery, OperationResult<PurchaseReturnOptionsDto>>
{
    public Task<OperationResult<PurchaseReturnOptionsDto>> Handle(GetPurchaseReturnOptionsQuery request, CancellationToken ct) =>
        service.ReadAsync(request.PurchaseOrderId, ct, request.IncludeAccepted);
}
