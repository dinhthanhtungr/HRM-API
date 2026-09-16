namespace HRM.Application.Commons.Authorization.PLM;

public static class PlmPolicies
{
    public const string ApproveSaleOrder = "PLM.SaleOrder.Approve";
    public const string ManageCustomerLabels = "PLM.CustomerLabels.Manage";
    public const string ViewFormulaDetail = "PLM.Formula.Detail.View";
    public const string ViewFormulaMaterials = "PLM.FormulaMaterials.View";
    public const string ViewFormulaPrices = "PLM.FormulaPrices.View";
    public const string ManageFormula = "PLM.Formula.Manage";
    public const string UpdateMaterialSupplierPrice = "PLM.MaterialSupplierPrice.Update";
    public const string ViewMaterialPriceReview = "PLM.MaterialPriceReview.View";
    public const string ManageMaterialPurchaseAvailability = "PLM.MaterialPurchaseAvailability.Manage";
    public const string ViewMaterialReplacements = "PLM.MaterialReplacement.View";
    public const string ManageMaterialReplacements = "PLM.MaterialReplacement.Manage";
    public const string UpdateFormulaPricing = "PLM.FormulaPricing.Update";
    public const string EditProductTechnicalInfo = "PLM.ProductTechnicalInfo.Edit";
    public const string SelectFormula = "PLM.Formula.Select";
    public const string ViewSampleProductionOrders = "PLM.SampleProductionOrder.View";
    public const string ManageSampleProductionOrders = "PLM.SampleProductionOrder.Manage";
    public const string ViewBom = "PLM.Bom.View";
    public const string ManageBomDraft = "PLM.Bom.Draft.Manage";
    public const string ReleaseBom = "PLM.Bom.Release";
    public const string ObsoleteBom = "PLM.Bom.Obsolete";
    public const string AssignStandardBom = "PLM.Bom.Standard.Assign";
    public const string ManageBomLossTypes = "PLM.Bom.LossTypes.Manage";
    public const string UpdateProductionLoss = "PLM.ProductionLoss.Update";
    public const string FinalizeProductionLoss = "PLM.ProductionLoss.Finalize";
}
