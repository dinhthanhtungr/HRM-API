using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Authorization.PLM;
using Microsoft.AspNetCore.Authorization;

namespace HRM.Api.Security.Authorization;

internal static class PlmAuthorizationPolicies
{
    internal static void AddPlmPolicies(this AuthorizationOptions options)
    {
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

        options.AddPolicy(PlmPolicies.EditProductTechnicalInfo, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.ProductTechnicalEditors);
        });

        options.AddPolicy(PlmPolicies.SelectFormula, policy =>
        {
            policy.RequireRole(ApplicationRoleSets.PLM.FormulaSelectors);
        });
    }
}
