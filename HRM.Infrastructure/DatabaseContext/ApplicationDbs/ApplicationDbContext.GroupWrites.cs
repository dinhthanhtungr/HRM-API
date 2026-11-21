using HRM.Domain.Entities.InternalMailSchema;
using HRM.Infrastructure.Services.InternalMail;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    private static readonly string[] GroupStructuralProperties =
        [nameof(InternalConversation.RelatedType), nameof(InternalConversation.RelatedId), nameof(InternalConversation.Subject), nameof(InternalConversation.IsActive)];

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveGroupAwareAsync(acceptAllChangesOnSuccess, default).GetAwaiter().GetResult();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        SaveGroupAwareAsync(acceptAllChangesOnSuccess, cancellationToken);

    /// <summary>Permanent write-through: conversation + group mapping commit or roll back together, across every EF writer.</summary>
    private async Task<int> SaveGroupAwareAsync(bool accept, CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        CaptureConversationSummaryChanges();
        ValidatePublishedMembershipRevisions();
        var changes = ChangeTracker.Entries<InternalConversation>()
            .Where(e => e.State == EntityState.Added || (e.State == EntityState.Modified && GroupStructuralProperties.Any(p => e.Property(p).IsModified)))
            .Select(e => (e.Entity.CompanyId, Id: e.Entity.InternalConversationId)).ToArray();
        if (changes.Length == 0 || !Database.IsNpgsql())
            return await base.SaveChangesAsync(accept, ct).ConfigureAwait(false);
        if (changes.Any(c => c.CompanyId == Guid.Empty || c.Id == Guid.Empty)) throw new InvalidOperationException("Conversation identity/company is required.");
        if (Database.CurrentTransaction is not null)
            return await SaveWithGroupTransactionAsync(changes, accept, ct).ConfigureAwait(false);
        return await Database.CreateExecutionStrategy().ExecuteAsync(() => SaveWithGroupTransactionAsync(changes, accept, ct)).ConfigureAwait(false);
    }

    private async Task<int> SaveWithGroupTransactionAsync((Guid CompanyId, Guid Id)[] changes, bool accept, CancellationToken ct)
    {
        await using var owned = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(ct).ConfigureAwait(false) : null;
        var transaction = Database.CurrentTransaction!;
        var savepoint = "group_write_" + Guid.NewGuid().ToString("N");
        if (owned is null) await transaction.CreateSavepointAsync(savepoint, ct).ConfigureAwait(false);
        try
        {
            foreach (var company in changes.Select(c => c.CompanyId).Distinct().Order())
                await InternalConversationGroupStore.LockCompanyAsync(this, company, ct).ConfigureAwait(false);
            var store = new InternalConversationGroupStore(this);
            // GROUP-STORAGE-TRANSITION: schema-readiness — old databases can run before migration; message-only saves never probe schema.
            var schema = await store.SchemaStateAsync(ct).ConfigureAwait(false);
            var result = await base.SaveChangesAsync(false, ct).ConfigureAwait(false);
            if (schema != 0)
                foreach (var company in changes.Select(c => c.CompanyId).Distinct().Order())
                {
                    var changedIds = changes.Where(c => c.CompanyId == company).Select(c => c.Id).Distinct().ToArray();
                    var sources = await store.SourcesAsync(company, changedIds, ct).ConfigureAwait(false);
                    var ids = sources.Select(c => c.Id).ToArray();
                    var mappings = await store.MappingsAsync(company, ids, false, ct).ConfigureAwait(false);
                    var issues = InternalConversationGroupPlan.Validate(sources, mappings, company, allowLifecycleUpdate: true);
                    if (issues.Count != 0) throw new InvalidOperationException("Group mapping validation failed: " + string.Join(",", issues));
                    await store.InsertMissingAsync(company, ids, ct).ConfigureAwait(false);
                    await store.UpdateMirrorsAsync(company, changedIds, ct).ConfigureAwait(false);
                }
            // GROUP-STORAGE-TRANSITION: legacy-bridge-detection — if an older trigger already inserted a mapping, ON CONFLICT preserves it.
            // The application writer still runs; schema-only installations need no trigger at all.
            if (owned is not null) await owned.CommitAsync(ct).ConfigureAwait(false);
            else await transaction.ReleaseSavepointAsync(savepoint, ct).ConfigureAwait(false);
            if (accept) ChangeTracker.AcceptAllChanges();
            return result;
        }
        catch
        {
            if (owned is not null) await owned.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            else await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
