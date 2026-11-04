using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CheckProductionOrder;

internal sealed class CheckProductionOrderCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock
) : IRequestHandler<CheckProductionOrderCommand, OperationResult>
{
    public async Task<OperationResult> Handle(CheckProductionOrderCommand request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.CheckProductionOrders, true);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        if (!request.IsPrintedStock.HasValue)
        {
            return OperationResult.Fail("IsPrintedStock phải là true hoặc false.");
        }

        var order = await db.MfgProductionOrders.FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.Id && x.CompanyId == user.CompanyId && x.IsActive, ct);
        if (order is null)
        {
            return OperationResult.Fail("Không tìm thấy lệnh sản xuất trong công ty.");
        }

        order.IsPrintedStock = request.IsPrintedStock;
        order.Checker = user.EmployeeId;
        order.CheckedDate = clock.Now;
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok("Kiểm tra lệnh sản xuất thành công.");
    }
}
