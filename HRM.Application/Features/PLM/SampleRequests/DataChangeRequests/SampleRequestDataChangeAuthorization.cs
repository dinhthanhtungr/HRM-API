using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;

internal static class SampleRequestDataChangeAuthorization
{
    private static readonly string[] RequesterRoles =
    [
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President,
        ApplicationRoles.Leader,
        ApplicationRoles.Sales.SaleUser
    ];

    private static readonly string[] ApproverRoles =
    [
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President,
        ApplicationRoles.Lab.LabUser
    ];

    public static bool CanRequest(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(RequesterRoles);

    public static bool CanRequestFor(
        ICurrentUser currentUser,
        Guid employeeId,
        Guid managerBy,
        Guid createdBy)
    {
        return currentUser.IsInAnyRole(ApplicationRoleSets.SuperUsers) ||
               currentUser.IsInRole(ApplicationRoles.Leader) ||
               managerBy == employeeId ||
               createdBy == employeeId;
    }

    public static bool CanApprove(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(ApproverRoles);
}
