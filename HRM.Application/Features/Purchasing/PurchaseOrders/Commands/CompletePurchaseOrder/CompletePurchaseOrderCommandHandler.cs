using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CompletePurchaseOrder;
internal sealed class CompletePurchaseOrderCommandHandler(PurchaseOrderWorkflowService workflow, ICurrentUserPermissionService permissions)
    : IRequestHandler<CompletePurchaseOrderCommand, OperationResult>
{
    public Task<OperationResult> Handle(CompletePurchaseOrderCommand request, CancellationToken ct) =>
        PurchaseOrderAccess.CanManage(permissions)
            ? workflow.CompleteAsync(request.PurchaseOrderId, ct)
            : Task.FromResult(OperationResult.Fail("Bạn không có quyền hoàn tất đơn mua hàng."));
}
