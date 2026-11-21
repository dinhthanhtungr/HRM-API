using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;

internal sealed class SaleOrderTrackingReader(
    IPLMReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
{
    private static readonly string[] DisplayStatuses =
    [
        nameof(MerchadiseStatus.New), nameof(MerchadiseStatus.Approved),
        nameof(MerchadiseStatus.Processing), nameof(MerchadiseStatus.Delivering),
        nameof(MerchadiseStatus.Delivered), nameof(MerchadiseStatus.Paused),
        nameof(MerchadiseStatus.Cancelled), nameof(MerchadiseStatus.Completed),
    ];

    internal IQueryable<MerchandiseOrder>? Read()
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewSaleOrderTracking) ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId.GetValueOrDefault() == Guid.Empty)
            return null;

        return db.MerchandiseOrders.AsNoTracking().Where(order =>
            order.CompanyId == companyId && order.IsActive && order.OrderType != OrderType.Internal &&
            order.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
            order.Customer != null && order.Customer.CompanyId == companyId &&
            order.Customer.IsActive == true && order.Customer.ExternalId != null &&
            order.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId);
    }

    internal static IQueryable<SaleOrderTrackingDto> Project(IQueryable<MerchandiseOrder> orders) =>
        orders.Select(order => new SaleOrderTrackingDto(
            order.MerchandiseOrderId, order.ExternalId, order.CustomerNameSnapshot,
            order.IsDeliveryPaused && order.Status != nameof(MerchadiseStatus.Cancelled) &&
            order.Status != nameof(MerchadiseStatus.Completed) && order.Status != nameof(MerchadiseStatus.Delivered)
                ? nameof(MerchadiseStatus.Paused)
                : DisplayStatuses.Contains(order.Status) ? order.Status : nameof(MerchadiseStatus.Processing),
            order.CreateDate));
}
