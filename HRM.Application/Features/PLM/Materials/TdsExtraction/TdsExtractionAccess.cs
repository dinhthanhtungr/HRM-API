using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

public sealed class TdsExtractionAccess(ICurrentUser currentUser, ICurrentUserPermissionService permissions)
{
    public bool CanExtract => currentUser.IsAuthenticated && currentUser.UserId != Guid.Empty &&
        currentUser.CompanyId is { } companyId && companyId != Guid.Empty &&
        permissions.HasPermission(ApplicationPermissions.PLM.ExtractMaterialDocuments);

    public TdsStartResult Start(ITdsJobQueue queue, IReadOnlyList<TdsInputFile>? uploads,
        int sourceOffset = 0, int? sourceLimit = null)
        => CanExtract ? queue.TryEnqueue(currentUser.CompanyId!.Value, currentUser.UserId, uploads, sourceOffset, sourceLimit)
            : new(false, null, "forbidden");

    public TdsJobDto? Get(ITdsJobQueue queue, Guid jobId)
        => CanExtract ? queue.Get(jobId, currentUser.CompanyId!.Value, currentUser.UserId) : null;

    public byte[]? GetExcel(ITdsJobQueue queue, Guid jobId)
        => CanExtract ? queue.GetExcel(jobId, currentUser.CompanyId!.Value, currentUser.UserId) : null;

    public bool Remove(ITdsJobQueue queue, Guid jobId)
        => CanExtract && queue.Remove(jobId, currentUser.CompanyId!.Value, currentUser.UserId);
}
