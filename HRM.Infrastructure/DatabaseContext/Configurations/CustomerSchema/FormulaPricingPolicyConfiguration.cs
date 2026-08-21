using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class FormulaPricingPolicyConfiguration : IEntityTypeConfiguration<FormulaPricingPolicy>
{
    public void Configure(EntityTypeBuilder<FormulaPricingPolicy> entity)
    {
        entity.ToTable("FormulaPricingPolicies", "Customer");
        entity.HasKey(x => x.FormulaPricingPolicyId).HasName("PK_FormulaPricingPolicies");
        entity.Property(x => x.FormulaPricingPolicyId).ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.Profile).HasConversion<int>();
        entity.Property(x => x.Currency).HasColumnType("citext").HasMaxLength(10)
            .HasDefaultValue("VND").IsRequired();
        entity.Property(x => x.Name).HasColumnType("citext").HasMaxLength(150).IsRequired();
        entity.Property(x => x.DefaultManufacturingCost).HasPrecision(22, 6);
        entity.Property(x => x.DefaultProfitMarginRate).HasPrecision(9, 4);
        entity.Property(x => x.RoundingRule).HasConversion<int>();
        entity.Property(x => x.RoundingIncrement).HasPrecision(22, 6);
        entity.Property(x => x.Status).HasConversion<int>()
            .HasDefaultValue(FormulaPricingPolicyStatus.Draft);
        entity.Property(x => x.Version).HasDefaultValue(1);
        entity.Property(x => x.IsActive).HasDefaultValue(true);

        entity.HasIndex(x => new { x.CompanyId, x.Profile, x.Currency, x.Version })
            .IsUnique().HasDatabaseName("UX_FormulaPricingPolicies_Company_Profile_Currency_Version");
        entity.HasIndex(x => new { x.CompanyId, x.Profile, x.Currency, x.Status, x.IsActive })
            .HasDatabaseName("IX_FormulaPricingPolicies_Current");

        entity.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_FormulaPricingPolicies_Company");
        entity.HasOne(x => x.CreatedByNavigation).WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_FormulaPricingPolicies_CreatedBy");
        entity.HasOne(x => x.UpdatedByNavigation).WithMany().HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_FormulaPricingPolicies_UpdatedBy");
        entity.HasOne(x => x.PublishedByNavigation).WithMany().HasForeignKey(x => x.PublishedBy)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_FormulaPricingPolicies_PublishedBy");
        entity.HasMany(x => x.Tiers).WithOne(x => x.FormulaPricingPolicy)
            .HasForeignKey(x => x.FormulaPricingPolicyId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FormulaPricingPolicyTiers_Policy");
    }
}
