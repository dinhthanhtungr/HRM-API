using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.NotifyPromisedDelivery;

internal sealed class NotifyPromisedDeliveryCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    PromisedDeliveryNotifier notifier) : IRequestHandler<NotifyPromisedDeliveryCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(NotifyPromisedDeliveryCommand request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageProductionOrders, true);
        if (error is not null) return OperationResult<Guid>.Fail(error);
        var externalId = request.ExternalId?.Trim();
        if (string.IsNullOrEmpty(externalId) || externalId.Length > 200 || request.PromisedDeliveryDate == default)
            return OperationResult<Guid>.Fail("Mã MFG và ngày hứa giao hợp lệ là bắt buộc.");

        var company = user.CompanyId!.Value;
        // Never pick an arbitrary order when a legacy external code is duplicated.
        var orders = await db.MfgProductionOrders.AsNoTracking()
            .Where(o => o.CompanyId == company && o.IsActive && o.ExternalId == externalId)
            .Select(o => new { o.MfgProductionOrderId, o.ExternalId, o.ExpectedDate }).Take(2).ToListAsync(ct);
        if (orders.Count != 1)
            return OperationResult<Guid>.Fail(orders.Count == 0 ? "Không tìm thấy lệnh MFG active trong công ty." : "Mã MFG không duy nhất trong công ty.");
        var order = orders[0];
        return await notifier.PublishAsync(order.MfgProductionOrderId, order.ExternalId, order.ExpectedDate,
            request.PromisedDeliveryDate.ToDateTime(TimeOnly.MinValue), ct);
    }
}
