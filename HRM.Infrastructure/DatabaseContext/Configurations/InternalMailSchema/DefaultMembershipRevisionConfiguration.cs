using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public sealed class DefaultMembershipRevisionConfiguration : IEntityTypeConfiguration<DefaultMembershipRevision>
{
    public void Configure(EntityTypeBuilder<DefaultMembershipRevision> e)
    {
        e.ToTable("DefaultMembershipRevisions", "InternalMail", t =>
        {
            t.ExcludeFromMigrations();
            t.HasCheckConstraint("CK_DmrVersion", "\"Version\" > 0");
            t.HasCheckConstraint("CK_DmrReason", "btrim(\"Reason\") <> ''");
        });
        e.HasKey(x => x.RevisionId).HasName("PK_DefaultMembershipRevisions");
        e.HasAlternateKey(x => new { x.CompanyId, x.PolicyId, x.RevisionId }).HasName("AK_DmrCompanyPolicyRevision");
        e.Property(x => x.RevisionId).ValueGeneratedNever();
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.HasIndex(x => new { x.PolicyId, x.Version }).IsUnique().HasDatabaseName("UX_DmrPolicyVersion");
        e.HasOne(x => x.Policy).WithMany().HasForeignKey(x => new { x.CompanyId, x.PolicyId })
            .HasPrincipalKey(x => new { x.CompanyId, x.PolicyId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<DefaultMembershipRevision>().WithMany().HasForeignKey(x => new { x.CompanyId, x.PolicyId, x.RestoredFromRevisionId })
            .HasPrincipalKey(x => new { x.CompanyId, x.PolicyId, x.RevisionId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
