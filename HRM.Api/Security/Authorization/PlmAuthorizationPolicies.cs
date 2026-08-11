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

        options.AddPolicy(PlmPolicies.ViewFormulaDetail, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.ProductTechnicalEditors);
        });

        options.AddPolicy(PlmPolicies.ViewFormulaMaterials, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaMaterialViewers);
        });

        options.AddPolicy(PlmPolicies.ViewFormulaPrices, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaPriceViewers);
        });

        options.AddPolicy(PlmPolicies.UpdateMaterialSupplierPrice, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.MaterialSupplierPriceEditors);
        });

        options.AddPolicy(PlmPolicies.ManageFormula, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaManagers);
        });

        options.AddPolicy(PlmPolicies.UpdateFormulaPricing, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaPricingEditors);
        });

        options.AddPolicy(PlmPolicies.EditProductTechnicalInfo, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.ProductTechnicalEditors);
        });

        options.AddPolicy(PlmPolicies.SelectFormula, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaSelectors);
        });

        options.AddPolicy(PlmPolicies.ViewSampleProductionOrders, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaMaterialViewers);
        });

        options.AddPolicy(PlmPolicies.ManageSampleProductionOrders, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.ProductTechnicalEditors);
        });
    }
}
