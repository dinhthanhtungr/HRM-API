using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingLossProfileConfiguration : IEntityTypeConfiguration<ManufacturingLossProfile>
{
    public void Configure(EntityTypeBuilder<ManufacturingLossProfile> entity)
    {
        entity.ToTable("manufacturing_loss_profiles", "bom", table =>
        {
            table.HasCheckConstraint(
                "ck_manufacturing_loss_profiles_effective_range",
                "effective_to IS NULL OR effective_from IS NULL OR effective_to >= effective_from");
        });
        entity.HasKey(x => x.ManufacturingLossProfileId).HasName("pk_manufacturing_loss_profiles");

        entity.Property(x => x.ManufacturingLossProfileId).HasColumnName("manufacturing_loss_profile_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
        entity.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.CreatedDate).HasColumnName("created_date").IsRequired();
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.ReleasedDate).HasColumnName("released_date");
        entity.Property(x => x.ReleasedBy).HasColumnName("released_by");
        entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        entity.HasIndex(x => new { x.CompanyId, x.ExternalId })
            .IsUnique().HasDatabaseName("ux_manufacturing_loss_profiles_company_code");
        entity.HasIndex(x => new { x.CompanyId, x.Status })
            .HasDatabaseName("ix_manufacturing_loss_profiles_company_status");

        entity.HasOne(x => x.Company).WithMany(x => x.ManufacturingLossProfiles).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_profiles_company");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_profiles_created_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReleasedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_profiles_released_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_profiles_updated_by");
    }
}
