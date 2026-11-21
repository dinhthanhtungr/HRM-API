using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Authorization.PLM;
using Microsoft.AspNetCore.Authorization;

namespace HRM.Api.Security.Authorization;

internal static class PlmAuthorizationPolicies
{
    internal static void AddPlmPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ComplaintPolicies.Create, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.ComplaintCreators));
        options.AddPolicy(ComplaintPolicies.Investigate, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.ComplaintInvestigators));
        options.AddPolicy(ComplaintPolicies.ActionUpdate, policy =>
            policy.RequireAuthenticatedUser());
        options.AddPolicy(ComplaintPolicies.Verify, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.ComplaintVerifiers));
        options.AddPolicy(ComplaintPolicies.InitialApprove, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.ComplaintApprovers));
        options.AddPolicy(ComplaintPolicies.FinalApprove, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.ComplaintApprovers));
        options.AddPolicy(ComplaintPolicies.ViewPdf, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.ComplaintPdfViewers));

        options.AddPolicy(PlmPolicies.ApproveSaleOrder, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.SaleOrderApprovers);
        });

        options.AddPolicy(PlmPolicies.ManageCustomerLabels, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.Modules.Sales);
        });

        options.AddPolicy(PlmPolicies.ViewFormulaDetail, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.ProductTechnicalViewers).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.ViewFormulaMaterials, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaMaterialViewers).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.ViewFormulaPrices, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaPriceViewers).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.UpdateMaterialSupplierPrice, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.MaterialSupplierPriceEditors).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.ManageMaterialPurchaseAvailability, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.MaterialPurchaseAvailabilityManagers);
        });

        options.AddPolicy(PlmPolicies.ViewMaterialReplacements, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.MaterialReplacementViewers);
        });

        options.AddPolicy(PlmPolicies.ManageMaterialReplacements, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.MaterialReplacementManagers);
        });

        options.AddPolicy(PlmPolicies.ManageFormula, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaManagers).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.UpdateFormulaPricing, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaPricingEditors).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.EditProductTechnicalInfo, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.ProductTechnicalEditors);
        });

        options.AddPolicy(PlmPolicies.SelectFormula, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaSelectors).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.ViewSampleProductionOrders, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaMaterialViewers).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.ManageSampleProductionOrders, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.ProductTechnicalEditors).RequireUnrestrictedAccount();
        });

        options.AddPolicy(PlmPolicies.ViewBom, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomViewers).RequireUnrestrictedAccount());
        options.AddPolicy(PlmPolicies.ManageBomDraft, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomDraftManagers).RequireUnrestrictedAccount());
        options.AddPolicy(PlmPolicies.ReleaseBom, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomReleasers).RequireUnrestrictedAccount());
        options.AddPolicy(PlmPolicies.ObsoleteBom, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomReleasers).RequireUnrestrictedAccount());
        options.AddPolicy(PlmPolicies.AssignStandardBom, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomStandardAssigners).RequireUnrestrictedAccount());
        options.AddPolicy(PlmPolicies.ManageBomLossTypes, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomLossManagers));
        options.AddPolicy(PlmPolicies.UpdateProductionLoss, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomLossManagers));
        options.AddPolicy(PlmPolicies.FinalizeProductionLoss, policy =>
            policy.RequireRole(ApplicationRoleSets.PLM.BomReleasers));
    }
}
