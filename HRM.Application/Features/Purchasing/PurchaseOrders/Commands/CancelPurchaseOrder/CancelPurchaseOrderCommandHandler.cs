using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CancelPurchaseOrder;
internal sealed class CancelPurchaseOrderCommandHandler(PurchaseOrderWorkflowService workflow, ICurrentUserPermissionService permissions)
    : IRequestHandler<CancelPurchaseOrderCommand, OperationResult>
{
    public Task<OperationResult> Handle(CancelPurchaseOrderCommand request, CancellationToken ct) =>
        PurchaseOrderAccess.CanManage(permissions)
            ? workflow.CancelAsync(request.PurchaseOrderId, request.Reason, ct)
            : Task.FromResult(OperationResult.Fail("Bạn không có quyền hủy đơn mua hàng."));
}
