namespace HRM.Application.Commons.Authorization;

/// <summary>Danh mục capability thực sự được backend hỗ trợ, dùng cho trình cấu hình role.</summary>
public static class ApplicationPermissionCatalog
{
    public static IReadOnlyList<string> Codes { get; } = Array.AsReadOnly(new[]
    {
        ApplicationPermissions.Equipment.View,
        ApplicationPermissions.Equipment.Create,
        ApplicationPermissions.Equipment.Update,
        ApplicationPermissions.Equipment.Delete,
        ApplicationPermissions.Pricing.ViewWorkbench,
        ApplicationPermissions.Pricing.ViewApprovedSellingPrice,
        ApplicationPermissions.Pricing.ViewSystemCalculatedPrice,
        ApplicationPermissions.Pricing.ViewMaterialCost,
        ApplicationPermissions.Pricing.ViewManufacturingCost,
        ApplicationPermissions.Pricing.ViewMargin,
        ApplicationPermissions.Pricing.ViewHistory,
        ApplicationPermissions.Pricing.Manage,
        ApplicationPermissions.Pricing.Approve,
        ApplicationPermissions.PLM.ViewFormulaPrices,
        ApplicationPermissions.PLM.ViewFormulaMaterials,
        ApplicationPermissions.PLM.ViewProductTechnicalInfo,
        ApplicationPermissions.PLM.ViewMaterialPriceReviewDetails,
        ApplicationPermissions.Dispatch.ViewDeliveryCost,
        ApplicationPermissions.Purchasing.ManagePurchaseOrders
    });

    public static IReadOnlyList<string> ResolveRole(
        string roleName, bool usesDatabasePermissions, IEnumerable<string> claims)
        => usesDatabasePermissions
            ? claims.Where(code => Codes.Contains(code, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : Codes.Where(code => ApplicationPermissionRoleSets.GetRoles(code)
                .Contains(roleName, StringComparer.OrdinalIgnoreCase)).ToArray();
}
