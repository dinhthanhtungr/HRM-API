using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.SubmitPurchaseOrder;
internal sealed class SubmitPurchaseOrderCommandHandler(PurchaseOrderWorkflowService workflow, ICurrentUserPermissionService permissions)
    : IRequestHandler<SubmitPurchaseOrderCommand, OperationResult>
{
    public Task<OperationResult> Handle(SubmitPurchaseOrderCommand request, CancellationToken ct) =>
        PurchaseOrderAccess.CanManage(permissions)
            ? workflow.SubmitAsync(request.PurchaseOrderId, ct)
            : Task.FromResult(OperationResult.Fail("Bạn không có quyền gửi đơn mua hàng."));
}
