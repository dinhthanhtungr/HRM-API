namespace HRM.Application.Commons.Authorization;

/// <summary>
/// Tên capability ổn định dùng trong Application. Role chỉ được ánh xạ vào các capability này
/// tại source of truth của authorization, không được dùng thay cho permission nghiệp vụ.
/// </summary>
public static class ApplicationPermissions
{
    public static class Pricing
    {
        public const string ViewWorkbench = "pricing.workbench.view";
        public const string ViewApprovedSellingPrice = "pricing.approved-selling-price.view";
        public const string ViewSystemCalculatedPrice = "pricing.system-calculated-price.view";
        public const string ViewMaterialCost = "pricing.material-cost.view";
        public const string ViewManufacturingCost = "pricing.manufacturing-cost.view";
        public const string ViewMargin = "pricing.margin.view";
        public const string ViewHistory = "pricing.history.view";
        public const string Manage = "pricing.manage";
        public const string Approve = "pricing.approve";
    }

    public static class PLM
    {
        public const string ViewFormulaPrices = "plm.formula-price.view";
        public const string ViewFormulaMaterials = "plm.formula-material.view";
        public const string ViewProductTechnicalInfo = "plm.product-technical-info.view";
        public const string ViewMaterialPriceReviewDetails = "plm.material-price-review.detail.view";
    }

    public static class Dispatch
    {
        public const string ViewDeliveryCost = "dispatch.delivery-cost.view";
    }

    public static class Purchasing
    {
        public const string ManagePurchaseOrders = "purchasing.purchase-order.manage";
    }

}
