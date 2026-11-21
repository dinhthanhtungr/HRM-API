using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Infrastructure.Services.PLM.ConversationSummary;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<SampleRequestConversationSummary> SampleRequestConversationSummaries => Set<SampleRequestConversationSummary>();

    private void CaptureConversationSummaryChanges()
    {
        if (!_conversationSummaryEnabled) return;
        // Only scalar source changes, excluding audit timestamps. No database read or product fan-out during SaveChanges.
        var sourceChanges = ChangeTracker.Entries().Where(e =>
            (e.Entity is SampleRequest || e.Entity is Product) &&
            (e.State == EntityState.Added || e.State == EntityState.Deleted ||
             e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified &&
                p.Metadata.Name is not ("UpdatedDate" or "UpdatedBy")))).ToArray();
        var jobs = sourceChanges.Select(change => change.Entity switch
        {
            SampleRequest s => new SummarySyncJob(s.CompanyId, s.SampleRequestId, "Request"),
            Product p => new SummarySyncJob(p.CompanyId, p.ProductId, "Product"),
            _ => throw new InvalidOperationException()
        }).Concat(ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State == EntityState.Added && e.Entity.SchemaName == "SampleRequests" &&
                e.Entity.TableName is "SampleRequests" or "Products")
            .Select(e => new SummarySyncJob(e.Entity.CompanyId ?? Guid.Empty, e.Entity.RecordId,
                e.Entity.TableName == "Products" ? "Product" : "Request"))).Distinct().ToArray();
        // Atomic ExecuteUpdate writers persist an AuditLog in their existing transaction.
        // Capturing its target covers schedule/deactivation/colour-name updates without querying 25k requests.
        foreach (var job in jobs)
        {
            if (job.CompanyId == Guid.Empty || job.EntityId == Guid.Empty) continue;
            var outbox = job.ToOutbox();
            if (!OutboxMessages.Local.Any(x => x.Type == SummarySyncJob.Type && x.ProcessedAt == null &&
                Entry(x).State == EntityState.Added && x.PayloadJson == outbox.PayloadJson)) OutboxMessages.Add(outbox);
        }
    }
}
