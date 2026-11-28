using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;

namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

public sealed class TdsExtractionAccess(ICurrentUser currentUser, ICurrentUserPermissionService permissions)
{
    public bool CanExtract => currentUser.IsAuthenticated && currentUser.UserId != Guid.Empty &&
        currentUser.CompanyId is { } companyId && companyId != Guid.Empty &&
        permissions.HasPermission(ApplicationPermissions.PLM.ExtractMaterialDocuments);

    public async Task<TdsStartResult> StartAsync(ITdsJobQueue queue, IPLMReadDbContext db, TdsExtractionOptions settings,
        IReadOnlyList<TdsInputFile>? uploads, CancellationToken ct, int sourceOffset = 0,
        int? sourceLimit = null, bool allSourceBatches = false)
    {
        if (!CanExtract) return new(false, null, "forbidden");
        if (!settings.Enabled) return new(false, null, "extraction_disabled");
        var sourceCompany = uploads is null && !settings.UsesSharedSource(allSourceBatches)
            ? await TdsSourceCompany.ResolveAsync(db, settings.SourceCompanyId, ct) : null;
        return queue.TryEnqueue(currentUser.CompanyId!.Value, currentUser.UserId, uploads, sourceOffset,
            sourceLimit, allSourceBatches, sourceCompany);
    }

    public TdsJobDto? Get(ITdsJobQueue queue, Guid jobId)
        => CanExtract ? queue.Get(jobId, currentUser.CompanyId!.Value, currentUser.UserId) : null;

    public byte[]? GetExcel(ITdsJobQueue queue, Guid jobId)
        => CanExtract ? queue.GetExcel(jobId, currentUser.CompanyId!.Value, currentUser.UserId) : null;

    public bool Remove(ITdsJobQueue queue, Guid jobId)
        => CanExtract && queue.Remove(jobId, currentUser.CompanyId!.Value, currentUser.UserId);
}
