using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public Task LockDefaultMembershipPolicyAsync(Guid companyId, InternalConversationGroupType groupType, CancellationToken ct) =>
        Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_advisory_xact_lock(hashtextextended({"internal-mail-default-membership:" + companyId.ToString("D") + ":" + (short)groupType}, 0))
            """, ct);

    private void ValidatePublishedMembershipRevisions()
    {
        var entries = ChangeTracker.Entries().Where(e => e.Entity is DefaultMembershipRevision or DefaultMembershipRule).ToArray();
        if (entries.Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Published membership revisions/rules are immutable; publish a new revision.");
        var newRevisions = entries.Where(e => e.State == EntityState.Added).Select(e => e.Entity).OfType<DefaultMembershipRevision>()
            .Select(e => (e.CompanyId, e.PolicyId, e.RevisionId)).ToHashSet();
        if (entries.Where(e => e.State == EntityState.Added).Select(e => e.Entity).OfType<DefaultMembershipRule>()
            .Any(e => !newRevisions.Contains((e.CompanyId, e.PolicyId, e.RevisionId))))
            throw new InvalidOperationException("Rules can only be inserted with their newly published revision.");
    }
}
