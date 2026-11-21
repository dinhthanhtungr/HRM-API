using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Application.Features.Reports.DeliveryOtd;

public static class DeliveryOtdAccess
{
    public static bool CanRead(ICurrentUser user, ICurrentUserPermissionService? permissions) =>
        ImsAccountRestrictions.AppliesTo(user)
            ? user.EmployeeId.GetValueOrDefault() != Guid.Empty &&
                permissions?.HasPermission(ApplicationPermissions.Reports.ViewDeliveryOtd) == true
            : DeliveryOrderAccessRules.CanRead(user);

    internal static IQueryable<Guid> VisibleOrderIds(IQueryable<MerchandiseOrder> orders, Guid company) =>
        orders.Where(order => order.CompanyId == company &&
                order.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
                order.Customer != null && order.Customer.CompanyId == company && order.Customer.IsActive == true &&
                order.Customer.ExternalId != null &&
                order.Customer.ExternalId.Trim().ToUpper() != InternalCustomerRules.InternalCustomerExternalId)
            .Select(order => order.MerchandiseOrderId);
}
