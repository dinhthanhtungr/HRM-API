using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.PLM.SampleRequests.Dtos;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>Repair only missing General watchers intended by a creation job. Existing membership and delivery preferences are immutable here.</summary>
public sealed class SilentWatcherReconciliationService(IPLMReadDbContext source, IInternalMailDbContext db,
    ICurrentUser user, ICurrentUserPermissionService permissions, SampleRequestRecipientResolver resolver,
    InternalMailAreaAccessService areas, ILogger<SilentWatcherReconciliationService> logger)
{
    public async Task<SilentWatcherReconciliationDto?> ExecuteAsync(long jobId, string? expectedFingerprint, bool apply, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || user.EmployeeId is not { } actor ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.ViewWorkflowNotificationJobs) ||
            !permissions.HasPermission(ApplicationPermissions.DevAndQA.RetryWorkflowNotificationJobs)) return null;
        if (apply && (expectedFingerprint?.Length != 64 || !expectedFingerprint.All(Uri.IsHexDigit))) return Blocked("InvalidPreview");

        var job = await WorkflowNotificationJobQueries.Scoped(source.OutboxMessages, company, null).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null) return Blocked("Unavailable");
        WorkflowNotificationPayload? payload;
        try { payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(job.PayloadJson); }
        catch (JsonException) { return Blocked("InvalidSource"); }
        if (payload is null || !payload.IsInitialMessage || payload.CompanyId != company) return Blocked("UnsupportedSource");
        var evt = await source.SampleRequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == company &&
            x.Id == payload.EventId && x.EventType == "SampleRequestWorkflowCreated", ct);
        if (evt?.RecordedByEmployeeId is not { } sender) return Blocked("InvalidSource");
        if (!await db.Employees.AsNoTracking().AnyAsync(e => e.CompanyId == company && e.EmployeeId == sender, ct))
            return Blocked("InvalidSource");
        var roots = await db.InternalConversations.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive &&
            x.RelatedType == InternalMailRelatedType.SampleRequest && x.RelatedId == evt.SampleRequestId)
            .Select(x => x.InternalConversationId).ToListAsync(ct);
        if (roots.Count != 1) return Blocked("Unavailable");
        var rootId = roots[0];
        await using var transaction = await db.BeginTransactionAsync(ct);
        if (apply) await db.LockInternalConversationAsync(company, rootId, ct);
        var sample = await db.SampleRequests.AsNoTracking().Where(s => s.CompanyId == company && s.IsActive &&
            s.SampleRequestId == evt.SampleRequestId && s.Product.CompanyId == company && s.Product.IsActive &&
            (s.Product.Category == null || s.Product.Category.CompanyId == company))
            .Select(s => new { s.ExternalId, s.ManagerBy, Category = s.Product.Category != null ? s.Product.Category.ExternalId : null })
            .SingleOrDefaultAsync(ct);
        if (sample is null || !await db.InternalConversations.AnyAsync(c => c.CompanyId == company && c.IsActive &&
            c.InternalConversationId == rootId && c.RelatedType == InternalMailRelatedType.SampleRequest && c.RelatedId == evt.SampleRequestId, ct))
            return Blocked("Unavailable");
        if (!await areas.GroupScopes(company).AnyAsync(g => g.ConversationId == rootId && g.RootConversationId == rootId &&
            g.GroupType == InternalConversationGroupType.General, ct)) return Blocked("Unavailable");

        // Null means omitted; an explicitly saved empty array must remain empty.
        var candidates = payload.SilentWatchers is not null ? payload.SilentWatchers.Distinct().ToArray()
            : payload.UseDefaultSilentWatchers ? (await resolver.ResolveDefaultSilentWatchersAsync(company, sender, ct)).Select(x => x.EmployeeId).ToArray()
            : [];
        var protectedIds = new HashSet<Guid>(payload.Recipients ?? []);
        protectedIds.UnionWith(payload.ExtraRecipients ?? []);
        protectedIds.Add(sender);
        protectedIds.Add(sample.ManagerBy);
        protectedIds.UnionWith((await resolver.ResolveDefaultMessageRecipientsAsync(company, sample.Category, ct)).Where(x => x.Locked).Select(x => x.EmployeeId));
        protectedIds.UnionWith((await resolver.ResolveSalesGroupLeaderRecipientsAsync(company, sender, ct)).Select(x => x.EmployeeId));
        protectedIds.UnionWith((await resolver.ResolveSalesGroupAdminRecipientsAsync(company, sender, ct)).Select(x => x.EmployeeId));
        var people = await db.Employees.AsNoTracking().Where(e => e.CompanyId == company && candidates.Contains(e.EmployeeId))
            .Select(e => new { e.EmployeeId, e.FullName }).ToDictionaryAsync(e => e.EmployeeId, ct);
        var eligible = await areas.Recipients(company, InternalMailAreas.General, rootId).Where(id => candidates.Contains(id)).ToHashSetAsync(ct);
        var existing = await db.InternalConversationParticipants.AsNoTracking().Where(p => p.InternalConversationId == rootId && candidates.Contains(p.EmployeeId))
            .ToDictionaryAsync(p => p.EmployeeId, ct);
        var rows = candidates.Where(people.ContainsKey).OrderBy(id => id).Select(id => new SilentWatcherReconciliationRow(id,
            people[id].FullName ?? "", SilentWatcherReconciliationRules.Status(eligible.Contains(id), protectedIds.Contains(id), existing.GetValueOrDefault(id)))).ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{company}|{rootId}|{jobId}|{payload.EventId}|{sender}|{payload.UseDefaultSilentWatchers}|" +
            string.Join(',', payload.SilentWatchers ?? []) + "|" + string.Join('|', rows.Select(x => $"{x.EmployeeId}:{x.Status}")))));
        var preview = new SilentWatcherReconciliationDto("Preview", sample.ExternalId, fingerprint, rows);
        if (!apply) return preview;
        var missing = rows.Where(r => r.Status == "Missing").Select(r => r.EmployeeId).ToArray();
        if (missing.Length == 0) return preview with { Outcome = "AlreadyCurrent" };
        if (!string.Equals(fingerprint, expectedFingerprint, StringComparison.Ordinal)) return preview with { Outcome = "PreviewChanged" };
        var added = await db.InsertMissingSilentWatchersAsync(company, rootId, missing, DateTime.Now, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Employee {ActorId} restored {Count} silent watchers for creation job {JobId} in company {CompanyId}", actor, added, jobId, company);
        return preview with { Outcome = "Restored", AddedCount = added, Fingerprint = null };
    }

    private static SilentWatcherReconciliationDto Blocked(string outcome) => new(outcome, null, null, []);
}
