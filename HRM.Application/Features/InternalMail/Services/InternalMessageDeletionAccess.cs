using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.InternalMailSchema;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>Developer moderation bypasses membership, but never company scope or permission revocation.</summary>
internal sealed class InternalMessageDeletionAccess(ICurrentUser user, ICurrentUserPermissionService permissions)
{
    public bool CanDelete => user.IsAuthenticated &&
        user.CompanyId is { } company && company != Guid.Empty &&
        user.EmployeeId is { } employee && employee != Guid.Empty &&
        user.IsInAnyRole(ApplicationRoleSets.InternalMail.MessageDeleters) &&
        permissions.HasPermission(ApplicationPermissions.InternalMail.DeleteMessage);

    public IQueryable<InternalMessage> Scope(IQueryable<InternalMessage> messages)
    {
        var allowed = CanDelete;
        var company = user.CompanyId;
        return messages.Where(message => allowed && !message.IsDeleted &&
            message.Conversation.CompanyId == company && message.Conversation.IsActive);
    }
}
