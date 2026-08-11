using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.ComplaintReports.Services;

public static class ComplaintAuthorizationRules
{
    public static bool CanCreate(ICurrentUser user)
        => user.IsInAnyRole(ApplicationRoleSets.PLM.ComplaintCreators);

    public static bool CanInvestigate(ICurrentUser user)
        => user.IsInAnyRole(ApplicationRoleSets.PLM.ComplaintInvestigators);

    public static bool CanInitialApprove(ICurrentUser user)
        => user.IsInAnyRole(ApplicationRoleSets.PLM.ComplaintApprovers);

    public static bool CanFinalApprove(ICurrentUser user)
        => user.IsInAnyRole(ApplicationRoleSets.PLM.ComplaintApprovers);

    public static bool CanVerify(ICurrentUser user)
        => user.IsInAnyRole(ApplicationRoleSets.PLM.ComplaintVerifiers);

    public static bool CanViewPdf(ICurrentUser user)
        => user.IsInAnyRole(ApplicationRoleSets.PLM.ComplaintPdfViewers);

    public static bool CanManageActions(ICurrentUser user)
        => CanInvestigate(user) || CanFinalApprove(user);

    public static bool CanUpdateAction(ICurrentUser user, Guid? personInChargeId)
        => personInChargeId.HasValue && personInChargeId == user.EmployeeId || CanManageActions(user);
}
