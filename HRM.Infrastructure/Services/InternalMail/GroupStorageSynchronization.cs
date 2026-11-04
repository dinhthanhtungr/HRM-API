using HRM.Application.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HRM.Infrastructure.Services.InternalMail;

// GROUP-STORAGE-TRANSITION: synchronization-api — only this service is temporary; the shared store/live writer remain.
public sealed class GroupStorageSynchronization(ApplicationDbContext db, IGroupStorageSyncExcelRenderer excel,
    ILogger<GroupStorageSynchronization> logger, PresidentMembershipBackfill presidents) : IGroupStorageSynchronization
{
    public async Task<GroupStorageSyncFile> SynchronizeAsync(Guid companyId, CancellationToken ct, bool includePresidentMemberships = true)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company is required.", nameof(companyId));
        var report = new GroupStorageSyncReport { CompanyId = companyId, IncludePresidentMemberships = includePresidentMemberships };
        var commitStarted = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='60s'", ct);
            await InternalConversationGroupStore.LockCompanyAsync(db, companyId, ct);
            var store = new InternalConversationGroupStore(db);
            var schema = await store.SchemaStateAsync(ct);
            var sources = await store.SourcesAsync(companyId, null, ct);
            if (sources.Count > InternalConversationGroupStore.MaximumExportRows)
                report.Issues.Add("TooManyGroupsNoChangesApplied");
            else
            {
                var ids = sources.Select(c => c.Id).ToArray();
                var mappings = schema == 0 ? [] : await store.MappingsAsync(companyId, ids, true, ct);
                if (schema == 0) report.Issues.Add("SchemaRequiredRunYourMigrationFirst");
                if (schema == 2) report.Issues.Add("LegacyTriggersInstalledUseSchemaOnlyDeployment");
                if (schema != 0) report.Issues.AddRange(InternalConversationGroupPlan.Validate(sources, mappings, companyId, allowLifecycleUpdate: false));
                var newlyInserted = new HashSet<Guid>();
                if (report.Issues.Count == 0)
                {
                    var existing = mappings.Select(g => g.ConversationId).ToHashSet();
                    newlyInserted = ids.Where(id => !existing.Contains(id)).ToHashSet();
                    await store.InsertMissingAsync(companyId, ids, ct);
                    var after = await store.MappingsAsync(companyId, ids, true, ct);
                    var issues = InternalConversationGroupPlan.Validate(sources, after, companyId, allowLifecycleUpdate: false);
                    if (after.Count != sources.Count || issues.Count != 0) throw new InvalidOperationException("Mapping verification failed.");
                    report.Outcome = "Completed";
                }
                await FillRowsAsync(report, sources, mappings, newlyInserted, ct);
                if (report.Outcome == "Completed" && includePresidentMemberships) await presidents.ApplyAsync(report, ct);
            }
            if (report.Outcome != "Completed") MarkUnapplied(report, false);
            // Export before commit: an export error cannot leave an applied run without a report.
            var file = File(report);
            if (report.Outcome == "Completed")
            {
                commitStarted = true;
                await transaction.CommitAsync(ct);
            }
            else await transaction.RollbackAsync(ct);
            return file;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is PostgresException or NpgsqlException or InvalidOperationException or DbUpdateException)
        {
            // No exception payload/SQL/connection details are logged or put into the exported workbook.
            logger.LogWarning("Group synchronization {RunId} failed with {FailureType}, SQLSTATE {SqlState}",
                report.RunId, exception.GetType().Name, (exception as PostgresException)?.SqlState ?? "none");
            report.Outcome = commitStarted ? "Unknown" : "Failed";
            report.Issues.Add(commitStarted ? "CommitOutcomeUnknownRetrySafely" : "RunRolledBackCheckSchemaConstraintsOrDatabaseAvailability");
            MarkUnapplied(report, commitStarted);
            return File(report);
        }
    }

    private static void MarkUnapplied(GroupStorageSyncReport report, bool unknown)
    {
        for (var i = 0; i < report.Rows.Count; i++)
            if (report.Rows[i].Result == "Mới đồng bộ") report.Rows[i] = report.Rows[i] with
                { Result = unknown ? "Chưa xác nhận; chạy lại an toàn" : "Không áp dụng (rollback)" };
        for (var i = 0; i < report.PresidentMemberships.Count; i++)
            if (report.PresidentMemberships[i].Status == PresidentMembershipSyncStatus.Added)
                report.PresidentMemberships[i] = report.PresidentMemberships[i] with
                { Status = unknown ? PresidentMembershipSyncStatus.Unknown : PresidentMembershipSyncStatus.RolledBack };
    }

    private GroupStorageSyncFile File(GroupStorageSyncReport report) => new(excel.Render(report),
        $"internal-mail-groups-{report.StartedAt:yyyyMMdd-HHmmss}-{report.RunId:N}.xlsx", report.Outcome, report.RunId);

    private async Task FillRowsAsync(GroupStorageSyncReport report, IReadOnlyList<ConversationGroupSource> sources,
        IReadOnlyList<InternalConversationGroup> before, HashSet<Guid> inserted, CancellationToken ct)
    {
        var company = report.CompanyId;
        var ids = sources.Select(c => c.Id).ToArray();
        var counts = await db.InternalMessages.AsNoTracking().Where(m => ids.Contains(m.InternalConversationId) && m.Conversation.CompanyId == company)
            .GroupBy(m => m.InternalConversationId).Select(g => new { Id = g.Key, Visible = g.Count(m => !m.IsDeleted), Deleted = g.Count(m => m.IsDeleted) })
            .ToDictionaryAsync(g => g.Id, ct);
        var samples = sources.Where(c => c.RelatedType == InternalMailRelatedType.SampleRequest && c.RelatedId.HasValue)
            .Select(c => c.RelatedId!.Value).ToArray();
        var colours = samples.Length == 0 ? [] : await db.SampleRequests.AsNoTracking().Where(s => s.CompanyId == company && samples.Contains(s.SampleRequestId))
            .Select(s => new { Id = s.SampleRequestId, Code = s.ExternalId, Colour = s.Product.CompanyId == company ? s.Product.ColourCode : null }).ToListAsync(ct);
        var roots = sources.ToDictionary(c => c.Id);
        var existing = before.ToDictionary(g => g.ConversationId);
        foreach (var c in sources)
        {
            var root = roots.GetValueOrDefault(c.RootId);
            var colour = colours.FirstOrDefault(s => root?.RelatedType == InternalMailRelatedType.SampleRequest && s.Id == root.RelatedId);
            var old = existing.GetValueOrDefault(c.Id);
            var visibleOld = old?.CompanyId == company ? old : null;
            var result = inserted.Contains(c.Id) ? "Mới đồng bộ" : old is null ? "Chưa đồng bộ" :
                old.CompanyId == company && old.RootConversationId == c.RootId && old.GroupType == c.Type && old.IsActive == c.IsActive ? "Đã có trước" : "Sai mapping";
            if (c.IsChild && (root is null || root.IsChild)) result = "Lỗi hồ sơ gốc";
            report.Rows.Add(new(c.Id, c.RootId, colour?.Code ?? root?.ExternalId ?? root?.Subject ?? c.ExternalId ?? c.Subject,
                colour?.Colour, c.Type, visibleOld?.DisplayName ?? (c.Type == InternalConversationGroupType.Custom ? c.Subject : c.Type switch
                    { InternalConversationGroupType.Technical => "Kỹ thuật", InternalConversationGroupType.Pricing => "Giá", _ => "Chung" }),
                c.IsActive, result, counts.GetValueOrDefault(c.Id)?.Visible ?? 0, counts.GetValueOrDefault(c.Id)?.Deleted ?? 0));
        }
    }
}
