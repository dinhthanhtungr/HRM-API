using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class ManufacturingBomStageConfiguration : IEntityTypeConfiguration<ManufacturingBomStage>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomStage> entity)
    {
        entity.ToTable("manufacturing_bom_stages", "bom", table =>
            table.HasCheckConstraint("ck_manufacturing_bom_stages_sequence_positive", "sequence_no > 0"));
        entity.HasKey(x => x.ManufacturingBomStageId).HasName("pk_manufacturing_bom_stages");

        entity.Property(x => x.ManufacturingBomStageId).HasColumnName("manufacturing_bom_stage_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.BomVersionId).HasColumnName("bom_version_id").IsRequired();
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        entity.HasIndex(x => new { x.BomVersionId, x.Code })
            .IsUnique().HasDatabaseName("ux_manufacturing_bom_stages_version_code");
        entity.HasIndex(x => new { x.BomVersionId, x.SequenceNo })
            .IsUnique().HasDatabaseName("ux_manufacturing_bom_stages_version_sequence");

        entity.HasOne(x => x.BomVersion).WithMany(x => x.ManufacturingStages).HasForeignKey(x => x.BomVersionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_stages_version");
    }
}
