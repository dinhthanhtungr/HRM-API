using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class ManufacturingLossTypeConfiguration : IEntityTypeConfiguration<ManufacturingLossType>
{
    public void Configure(EntityTypeBuilder<ManufacturingLossType> entity)
    {
        entity.ToTable("manufacturing_loss_types", "bom");
        entity.HasKey(x => x.ManufacturingLossTypeId).HasName("pk_manufacturing_loss_types");

        entity.Property(x => x.ManufacturingLossTypeId).HasColumnName("manufacturing_loss_type_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.DefaultCalculationMethod).HasColumnName("default_calculation_method")
            .HasConversion<string>().HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.IsRecoverable).HasColumnName("is_recoverable").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.Property(x => x.CreatedDate).HasColumnName("created_date").IsRequired();
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        entity.HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique().HasDatabaseName("ux_manufacturing_loss_types_company_code");
        entity.HasIndex(x => new { x.CompanyId, x.IsActive })
            .HasDatabaseName("ix_manufacturing_loss_types_company_active");

        entity.HasOne(x => x.Company).WithMany(x => x.ManufacturingLossTypes).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_types_company");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_types_created_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_types_updated_by");
    }
}
