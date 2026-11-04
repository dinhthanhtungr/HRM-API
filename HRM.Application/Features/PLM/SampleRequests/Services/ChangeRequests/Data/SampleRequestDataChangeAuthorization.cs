using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;

internal static class SampleRequestDataChangeAuthorization
{
    private static readonly string[] ApproverRoles =
    [
        ApplicationRoles.Admin,
        ApplicationRoles.Developer,
        ApplicationRoles.President,
        ApplicationRoles.Lab.LabUser
    ];

    public static bool CanApprove(ICurrentUser currentUser)
        => currentUser.IsInAnyRole(ApproverRoles);
}
