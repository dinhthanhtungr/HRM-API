using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Authorization.PLM;

internal sealed class PLMFieldVisibilityService : IPLMFieldVisibilityService
{
    private readonly ICurrentUser _currentUser;

    public PLMFieldVisibilityService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public bool CanViewFormulaPrices()
    {
        return _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaPriceViewers);
    }

    public bool CanViewFormulaMaterials()
    {
        return _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaMaterialViewers);
    }
}
