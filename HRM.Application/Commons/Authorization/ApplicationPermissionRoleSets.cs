namespace HRM.Application.Commons.Authorization;

/// <summary>
/// Ánh xạ tập trung từ capability sang role hiện hành. Đây là điểm thay thế khi chuyển
/// sang permission lưu trong database mà không làm thay đổi các feature đang sử dụng capability.
/// </summary>
public static class ApplicationPermissionRoleSets
{
    public static IReadOnlyCollection<string> GetRoles(string permission)
        => permission switch
        {
            ApplicationPermissions.Equipment.View => ApplicationRoleSets.Equipment.Viewers,
            ApplicationPermissions.Equipment.Create => ApplicationRoleSets.Equipment.Editors,
            ApplicationPermissions.Equipment.Update => ApplicationRoleSets.Equipment.Editors,
            ApplicationPermissions.Equipment.Delete => ApplicationRoleSets.Equipment.Deleters,
            ApplicationPermissions.Pricing.ViewWorkbench =>
                ApplicationRoleSets.Pricing.WorkbenchViewers,
            ApplicationPermissions.Pricing.ViewApprovedSellingPrice =>
                ApplicationRoleSets.Pricing.ApprovedSellingPriceViewers,
            ApplicationPermissions.Pricing.ViewSystemCalculatedPrice =>
                ApplicationRoleSets.Pricing.SystemCalculatedPriceViewers,
            ApplicationPermissions.Pricing.ViewMaterialCost =>
                ApplicationRoleSets.Pricing.MaterialCostViewers,
            ApplicationPermissions.Pricing.ViewManufacturingCost =>
                ApplicationRoleSets.Pricing.ManufacturingCostViewers,
            ApplicationPermissions.Pricing.ViewMargin =>
                ApplicationRoleSets.Pricing.MarginViewers,
            ApplicationPermissions.Pricing.ViewHistory =>
                ApplicationRoleSets.Pricing.HistoryViewers,
            ApplicationPermissions.Pricing.Manage =>
                ApplicationRoleSets.Pricing.Managers,
            ApplicationPermissions.Pricing.Approve =>
                ApplicationRoleSets.Pricing.Approvers,
            ApplicationPermissions.PLM.ViewFormulaPrices =>
                ApplicationRoleSets.PLM.FormulaPriceViewers,
            ApplicationPermissions.PLM.ViewFormulaMaterials =>
                ApplicationRoleSets.PLM.FormulaMaterialViewers,
            ApplicationPermissions.PLM.ViewProductTechnicalInfo =>
                ApplicationRoleSets.PLM.ProductTechnicalEditors,
            ApplicationPermissions.PLM.ViewMaterialPriceReviewDetails =>
                ApplicationRoleSets.PLM.MaterialPriceReviewDetailViewers,
            ApplicationPermissions.Dispatch.ViewDeliveryCost =>
                ApplicationRoleSets.Dispatch.DeliveryCostViewers,
            ApplicationPermissions.Purchasing.ManagePurchaseOrders =>
                ApplicationRoleSets.Modules.Purchasing,
            _ => []
        };
}
