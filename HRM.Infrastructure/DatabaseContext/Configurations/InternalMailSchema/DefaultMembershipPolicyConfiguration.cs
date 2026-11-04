using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public sealed class DefaultMembershipPolicyConfiguration : IEntityTypeConfiguration<DefaultMembershipPolicy>
{
    public void Configure(EntityTypeBuilder<DefaultMembershipPolicy> e)
    {
        e.ToTable("DefaultMembershipPolicies", "InternalMail", t =>
        {
            // Runtime model: operator creates schema via the separately reviewed migration-authoring repository.
            t.ExcludeFromMigrations();
            t.HasCheckConstraint("CK_DmpKind", "\"GroupType\" IN (1,2)");
            t.HasCheckConstraint("CK_DmpVersion", "(\"Version\" = 0 AND \"ActiveRevisionId\" IS NULL) OR (\"Version\" > 0 AND \"ActiveRevisionId\" IS NOT NULL)");
        });
        e.HasKey(x => x.PolicyId).HasName("PK_DefaultMembershipPolicies");
        e.HasAlternateKey(x => new { x.CompanyId, x.PolicyId }).HasName("AK_DmpCompanyPolicy");
        e.Property(x => x.PolicyId).ValueGeneratedNever();
        e.Property(x => x.GroupType).HasConversion<short>();
        e.Property(x => x.Version).IsConcurrencyToken();
        e.HasIndex(x => new { x.CompanyId, x.GroupType }).IsUnique().HasDatabaseName("UX_DmpCompanyKind");
        e.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ActiveRevision).WithMany()
            .HasForeignKey(x => new { x.CompanyId, x.PolicyId, x.ActiveRevisionId })
            .HasPrincipalKey(x => new { x.CompanyId, x.PolicyId, x.RevisionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DmpActiveRevision");
    }
}
