using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Authorization.PLM;

public static class MaterialSupplierPriceAccess
{
    // Same role source as PLM.MaterialSupplierPrice.Update; catalog capability alone is insufficient.
    public static bool CanUpdate(ICurrentUser user) => user.IsAuthenticated &&
        ApplicationRoleSets.PLM.MaterialSupplierPriceEditors.Any(user.IsInRole);
}
