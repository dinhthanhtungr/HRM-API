using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;

internal static class SampleRequestFormulaChangeAuthorization
{
    public static bool CanRequest(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors);

    public static bool CanApproveOrReject(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaSelectors);

    public static bool CanCancel(ICurrentUser currentUser, Guid employeeId, Guid requestedByEmployeeId)
    {
        return currentUser.IsInAnyRole(ApplicationRoleSets.SuperUsers) ||
               currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors) ||
               requestedByEmployeeId == employeeId;
    }
}
