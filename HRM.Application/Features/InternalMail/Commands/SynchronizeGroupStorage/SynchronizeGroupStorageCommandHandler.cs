using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.GroupStorage;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.SynchronizeGroupStorage;

// GROUP-STORAGE-TRANSITION: synchronization-api — authorization must happen before any DB query or Excel generation.
internal sealed class SynchronizeGroupStorageCommandHandler(ICurrentUser user, ICurrentUserPermissionService permissions,
    IGroupStorageSynchronization synchronization) : IRequestHandler<SynchronizeGroupStorageCommand, GroupStorageSyncFile?>
{
    public Task<GroupStorageSyncFile?> Handle(SynchronizeGroupStorageCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } employee || employee == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.InternalMail.SynchronizeGroupStorage))
            return Task.FromResult<GroupStorageSyncFile?>(null);
        return ExecuteAsync(company, request.IncludePresidentMemberships, ct);
    }

    private async Task<GroupStorageSyncFile?> ExecuteAsync(Guid company, bool includePresidentMemberships, CancellationToken ct) =>
        await synchronization.SynchronizeAsync(company, ct, includePresidentMemberships);
}
