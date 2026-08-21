using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class ManufacturingBomLossRuleConfiguration : IEntityTypeConfiguration<ManufacturingBomLossRule>
{
    public void Configure(EntityTypeBuilder<ManufacturingBomLossRule> entity)
    {
        entity.ToTable("manufacturing_bom_loss_rules", "bom", table =>
        {
            table.HasCheckConstraint("ck_manufacturing_bom_loss_rules_sequence_positive", "sequence_no > 0");
            table.HasCheckConstraint("ck_manufacturing_bom_loss_rules_rate_range", "rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent < 100)");
            table.HasCheckConstraint("ck_manufacturing_bom_loss_rules_fixed_nonnegative", "fixed_quantity_kg IS NULL OR fixed_quantity_kg >= 0");
            table.HasCheckConstraint("ck_manufacturing_bom_loss_rules_event_quantity_nonnegative", "quantity_per_event_kg IS NULL OR quantity_per_event_kg >= 0");
            table.HasCheckConstraint("ck_manufacturing_bom_loss_rules_event_count_nonnegative", "default_event_count IS NULL OR default_event_count >= 0");
        });
        entity.HasKey(x => x.ManufacturingBomLossRuleId).HasName("pk_manufacturing_bom_loss_rules");

        entity.Property(x => x.ManufacturingBomLossRuleId).HasColumnName("manufacturing_bom_loss_rule_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.BomVersionId).HasColumnName("bom_version_id").IsRequired();
        entity.Property(x => x.ManufacturingLossTypeId).HasColumnName("manufacturing_loss_type_id").IsRequired();
        entity.Property(x => x.BomVersionItemId).HasColumnName("bom_version_item_id");
        entity.Property(x => x.ManufacturingBomStageId).HasColumnName("manufacturing_bom_stage_id");
        entity.Property(x => x.CalculationMethod).HasColumnName("calculation_method")
            .HasConversion<string>().HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.RatePercent).HasColumnName("rate_percent").HasPrecision(9, 6);
        entity.Property(x => x.FixedQuantityKg).HasColumnName("fixed_quantity_kg").HasPrecision(18, 3);
        entity.Property(x => x.QuantityPerEventKg).HasColumnName("quantity_per_event_kg").HasPrecision(18, 3);
        entity.Property(x => x.DefaultEventCount).HasColumnName("default_event_count");
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.IsRecoverable).HasColumnName("is_recoverable").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.IncludeInMaterialRequest).HasColumnName("include_in_material_request").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");

        entity.HasIndex(x => new { x.BomVersionId, x.SequenceNo })
            .IsUnique().HasDatabaseName("ux_manufacturing_bom_loss_rules_version_sequence");
        entity.HasIndex(x => x.ManufacturingLossTypeId).HasDatabaseName("ix_manufacturing_bom_loss_rules_type");
        entity.HasIndex(x => x.BomVersionItemId).HasDatabaseName("ix_manufacturing_bom_loss_rules_item");
        entity.HasIndex(x => x.ManufacturingBomStageId).HasDatabaseName("ix_manufacturing_bom_loss_rules_stage");
        entity.HasIndex(x => new { x.BomVersionId, x.IsActive })
            .HasDatabaseName("ix_manufacturing_bom_loss_rules_version_active");

        entity.HasOne(x => x.BomVersion).WithMany(x => x.LossRules).HasForeignKey(x => x.BomVersionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_loss_rules_version");
        entity.HasOne(x => x.LossType).WithMany(x => x.LossRules).HasForeignKey(x => x.ManufacturingLossTypeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_loss_rules_type");
        entity.HasOne(x => x.BomVersionItem).WithMany(x => x.LossRules).HasForeignKey(x => x.BomVersionItemId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_loss_rules_item");
        entity.HasOne(x => x.ManufacturingStage).WithMany(x => x.LossRules).HasForeignKey(x => x.ManufacturingBomStageId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_bom_loss_rules_stage");
    }
}
