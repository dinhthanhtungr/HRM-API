using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Boms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class BomVersionConfiguration : IEntityTypeConfiguration<BomVersion>
{
    public void Configure(EntityTypeBuilder<BomVersion> entity)
    {
        entity.ToTable("bom_versions", "bom", table =>
        {
            table.HasCheckConstraint("ck_bom_versions_version_no_positive", "version_no > 0");
            table.HasCheckConstraint("ck_bom_versions_base_output_positive", "base_output_quantity > 0");
            table.HasCheckConstraint("ck_bom_versions_effective_period", "effective_to IS NULL OR effective_from IS NULL OR effective_to > effective_from");
        });
        entity.HasKey(x => x.BomVersionId).HasName("pk_bom_versions");

        entity.Property(x => x.BomVersionId).HasColumnName("bom_version_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.BomDefinitionId).HasColumnName("bom_definition_id").IsRequired();
        entity.Property(x => x.VersionNo).HasColumnName("version_no").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("citext")
            .HasMaxLength(32).HasDefaultValue(BomVersionStatus.Draft).IsRequired();
        entity.Property(x => x.BaseOutputQuantity).HasColumnName("base_output_quantity").HasPrecision(18, 3).IsRequired();
        entity.Property(x => x.OutputUnit).HasColumnName("output_unit").HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.SourceEngineeringBomVersionId).HasColumnName("source_engineering_bom_version_id");
        entity.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
        entity.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        entity.Property(x => x.ChangeReason).HasColumnName("change_reason").HasColumnType("text");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.CreatedDate).HasColumnName("created_date").IsRequired();
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.ReleasedDate).HasColumnName("released_date");
        entity.Property(x => x.ReleasedBy).HasColumnName("released_by");

        entity.HasIndex(x => new { x.BomDefinitionId, x.VersionNo })
            .IsUnique().HasDatabaseName("ux_bom_versions_definition_version_no");
        entity.HasIndex(x => new { x.BomDefinitionId, x.Status })
            .HasDatabaseName("ix_bom_versions_definition_status");
        entity.HasIndex(x => x.SourceEngineeringBomVersionId)
            .HasDatabaseName("ix_bom_versions_source_engineering");

        entity.HasOne(x => x.BomDefinition).WithMany(x => x.Versions).HasForeignKey(x => x.BomDefinitionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_versions_definition");
        entity.HasOne(x => x.SourceEngineeringBomVersion).WithMany(x => x.DerivedManufacturingBomVersions)
            .HasForeignKey(x => x.SourceEngineeringBomVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bom_versions_source_engineering");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_versions_created_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReleasedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_versions_released_by");
    }
}
