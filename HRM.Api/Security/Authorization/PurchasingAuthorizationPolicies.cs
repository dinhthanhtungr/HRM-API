using HRM.Application.Commons.Authorization;
using Microsoft.AspNetCore.Authorization;
namespace HRM.Api.Security.Authorization;
internal static class PurchasingAuthorizationPolicies
{
    internal static void AddPurchasingPolicies(this AuthorizationOptions options) => options.AddPolicy(PurchasingPolicies.ManagePurchaseOrders, policy => policy.RequireRole(ApplicationRoleSets.Modules.Purchasing));
}
