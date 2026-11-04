using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Services;

internal static class MaterialPlanningScope
{
    public static bool CanRead(ICurrentUser user, ICurrentUserPermissionService permissions)
        => user.IsAuthenticated && user.CompanyId is { } company && company != Guid.Empty &&
           permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders);

    public static IQueryable<MerchandiseOrder> Orders(IPLMReadDbContext db, Guid company)
        => db.MerchandiseOrders.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive);

    public static IQueryable<MerchandiseOrderDetail> Lines(IPLMReadDbContext db, Guid company)
        => db.MerchandiseOrderDetails.AsNoTracking().Where(x => x.IsActive &&
            Orders(db, company).Any(o => o.MerchandiseOrderId == x.MerchandiseOrderId));
}
