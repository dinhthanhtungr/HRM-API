using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseReturn;
internal sealed class CreatePurchaseReturnCommandHandler(PurchaseOrderReturnService service)
    : IRequestHandler<CreatePurchaseReturnCommand, OperationResult<PurchaseReturnRequestDto>>
{
    public Task<OperationResult<PurchaseReturnRequestDto>> Handle(CreatePurchaseReturnCommand request, CancellationToken ct) =>
        service.CreateAsync(request.PurchaseOrderId, request.Request, ct);
}
