using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.InternalMailSchema;

public sealed class DefaultMembershipRuleConfiguration : IEntityTypeConfiguration<DefaultMembershipRule>
{
    public void Configure(EntityTypeBuilder<DefaultMembershipRule> e)
    {
        e.ToTable("DefaultMembershipRules", "InternalMail", t =>
        {
            t.ExcludeFromMigrations();
            t.HasCheckConstraint("CK_DmRuleSource", "(\"SourceType\" IN (0,1) AND \"RoleId\" IS NOT NULL AND \"GroupTypeCode\" IS NULL) OR (\"SourceType\" = 2 AND \"RoleId\" IS NULL AND \"GroupTypeCode\" IS NOT NULL AND btrim(\"GroupTypeCode\") <> '')");
            t.HasCheckConstraint("CK_DmRuleCategory", "(\"CategoryScope\" IN (0,2) AND \"ProductCategoryId\" IS NULL) OR (\"CategoryScope\" = 1 AND \"ProductCategoryId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_DmRuleFallback", "\"CategoryScope\" <> 2 OR \"SourceType\" = 2");
        });
        e.HasKey(x => x.RuleId).HasName("PK_DefaultMembershipRules");
        e.Property(x => x.RuleId).ValueGeneratedNever();
        e.Property(x => x.SourceType).HasConversion<short>();
        e.Property(x => x.CategoryScope).HasConversion<short>();
        e.Property(x => x.GroupTypeCode).HasMaxLength(64);
        e.HasOne(x => x.Revision).WithMany(x => x.Rules).HasForeignKey(x => new { x.CompanyId, x.PolicyId, x.RevisionId })
            .HasPrincipalKey(x => new { x.CompanyId, x.PolicyId, x.RevisionId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Category>().WithMany().HasForeignKey(x => x.ProductCategoryId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.RevisionId, x.SourceType, x.RoleId, x.GroupTypeCode, x.CategoryScope, x.ProductCategoryId })
            .IsUnique().AreNullsDistinct(false).HasDatabaseName("UX_DmRuleIdentity");
    }
}
