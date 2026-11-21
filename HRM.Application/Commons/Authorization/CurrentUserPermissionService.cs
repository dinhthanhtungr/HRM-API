using HRM.Application.Abstractions.Security;

namespace HRM.Application.Commons.Authorization;

internal sealed class CurrentUserPermissionService : ICurrentUserPermissionService
{
    private readonly ICurrentUser _currentUser;

    public CurrentUserPermissionService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) || !_currentUser.IsAuthenticated)
        {
            return false;
        }

        if (ImsAccountRestrictions.AppliesTo(_currentUser) && ImsAccountRestrictions.BlocksPermission(permission))
        {
            return false;
        }

        // Developer operates the Sample Request workflow across owners; data scope and lifecycle gates remain in each use case.
        if (_currentUser.IsInRole(ApplicationRoles.Developer) &&
            (string.Equals(permission, ApplicationPermissions.PLM.ManageSampleSaleWorkflow, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(permission, ApplicationPermissions.PLM.ManageSampleLabWorkflow, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(permission, ApplicationPermissions.PLM.ConfirmCustomerPrice, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(permission, ApplicationPermissions.PLM.CorrectSampleTrialFeedback, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Lab work dates/receipt are role-granted, including tokens issued before this policy change.
        if ((string.Equals(permission, ApplicationPermissions.PLM.ManageSampleWorkSchedule, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(permission, ApplicationPermissions.PLM.ReceiveLabStandard, StringComparison.OrdinalIgnoreCase)) &&
            _currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleWorkLabOperators))
            return true;

        // Developer can confirm CRM sending even with an older explicit permission token.
        // LabAdmin thực hiện nghiệp vụ Lab như LabUser, kể cả token cấp trước khi bổ sung claim.
        if ((string.Equals(permission, ApplicationPermissions.PLM.ManageSampleLabWorkflow, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(permission, ApplicationPermissions.PLM.ViewFormulaMaterials, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(permission, ApplicationPermissions.PLM.ViewProductTechnicalInfo, StringComparison.OrdinalIgnoreCase)) &&
            _currentUser.IsInRole(ApplicationRoles.Lab.LabAdmin))
            return true;

        if (string.Equals(permission, ApplicationPermissions.Quotations.ConfirmSent, StringComparison.OrdinalIgnoreCase) &&
            _currentUser.IsInRole(ApplicationRoles.Developer))
            return true;

        // Quyền mua hàng của PLPUUser được cấp theo role, kể cả token explicit chưa có claim.
        if (string.Equals(permission, ApplicationPermissions.Purchasing.ManagePurchaseOrders, StringComparison.OrdinalIgnoreCase) &&
            _currentUser.IsInRole(ApplicationRoles.Production.PLPUUser))
            return true;

        // Quan sát hội thoại công ty gắn với role Developer, tương thích cả token explicit cũ.
        if (string.Equals(permission, ApplicationPermissions.InternalMail.ObserveCompanyConversations, StringComparison.OrdinalIgnoreCase))
            return _currentUser.IsInAnyRole(ApplicationRoleSets.InternalMail.CompanyConversationObservers);
        if (string.Equals(permission, ApplicationPermissions.InternalMail.SendCompanyMessages, StringComparison.OrdinalIgnoreCase))
            return _currentUser.IsInAnyRole(ApplicationRoleSets.InternalMail.CompanyMessageSenders);

        // GROUP-STORAGE-TRANSITION: synchronization-api — preserve old Developer tokens; a claim alone cannot grant this maintenance action.
        if (string.Equals(permission, ApplicationPermissions.InternalMail.SynchronizeGroupStorage, StringComparison.OrdinalIgnoreCase))
            return _currentUser.IsInAnyRole(ApplicationRoleSets.InternalMail.GroupStorageAdministrators);

        if (_currentUser.HasExplicitPermissionSet)
        {
            return _currentUser.Permissions.Contains(
                permission,
                StringComparer.OrdinalIgnoreCase);
        }

        // Compatibility path for access tokens issued before role permissions were seeded.
        return _currentUser.IsInAnyRole(ApplicationPermissionRoleSets.GetRoles(permission));
    }
}
