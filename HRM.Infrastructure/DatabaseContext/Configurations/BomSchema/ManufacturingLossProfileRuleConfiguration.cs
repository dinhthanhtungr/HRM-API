using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingLossProfileRuleConfiguration : IEntityTypeConfiguration<ManufacturingLossProfileRule>
{
    public void Configure(EntityTypeBuilder<ManufacturingLossProfileRule> entity)
    {
        entity.ToTable("manufacturing_loss_profile_rules", "bom", table =>
        {
            table.HasCheckConstraint("ck_manufacturing_loss_profile_rules_sequence_positive", "sequence_no > 0");
            table.HasCheckConstraint("ck_manufacturing_loss_profile_rules_rate_range", "rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent < 100)");
            table.HasCheckConstraint("ck_manufacturing_loss_profile_rules_fixed_nonnegative", "fixed_quantity_kg IS NULL OR fixed_quantity_kg >= 0");
            table.HasCheckConstraint("ck_manufacturing_loss_profile_rules_event_quantity_nonnegative", "quantity_per_event_kg IS NULL OR quantity_per_event_kg >= 0");
            table.HasCheckConstraint("ck_manufacturing_loss_profile_rules_event_count_nonnegative", "default_event_count IS NULL OR default_event_count >= 0");
            table.HasCheckConstraint(
                "ck_manufacturing_loss_profile_rules_scope_target",
                "(scope = 'Material' AND material_id IS NOT NULL AND target_stage_code IS NULL AND from_stage_code IS NULL AND to_stage_code IS NULL) OR " +
                "(scope = 'Stage' AND material_id IS NULL AND target_stage_code IS NOT NULL AND from_stage_code IS NULL AND to_stage_code IS NULL) OR " +
                "(scope = 'Transition' AND material_id IS NULL AND target_stage_code IS NULL AND from_stage_code IS NOT NULL AND to_stage_code IS NOT NULL) OR " +
                "(scope = 'OverallBom' AND material_id IS NULL AND target_stage_code IS NULL AND from_stage_code IS NULL AND to_stage_code IS NULL)");
        });
        entity.HasKey(x => x.ManufacturingLossProfileRuleId).HasName("pk_manufacturing_loss_profile_rules");

        entity.Property(x => x.ManufacturingLossProfileRuleId).HasColumnName("manufacturing_loss_profile_rule_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingLossProfileId).HasColumnName("manufacturing_loss_profile_id").IsRequired();
        entity.Property(x => x.ManufacturingLossTypeId).HasColumnName("manufacturing_loss_type_id").IsRequired();
        entity.Property(x => x.MaterialId).HasColumnName("material_id");
        entity.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.TargetStageCode).HasColumnName("target_stage_code").HasColumnType("citext").HasMaxLength(64);
        entity.Property(x => x.FromStageCode).HasColumnName("from_stage_code").HasColumnType("citext").HasMaxLength(64);
        entity.Property(x => x.ToStageCode).HasColumnName("to_stage_code").HasColumnType("citext").HasMaxLength(64);
        entity.Property(x => x.AllocationMethod).HasColumnName("allocation_method").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.CalculationMethod).HasColumnName("calculation_method").HasConversion<string>().HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.RatePercent).HasColumnName("rate_percent").HasPrecision(9, 6);
        entity.Property(x => x.FixedQuantityKg).HasColumnName("fixed_quantity_kg").HasPrecision(18, 3);
        entity.Property(x => x.QuantityPerEventKg).HasColumnName("quantity_per_event_kg").HasPrecision(18, 3);
        entity.Property(x => x.DefaultEventCount).HasColumnName("default_event_count");
        entity.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
        entity.Property(x => x.IsRecoverable).HasColumnName("is_recoverable").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.IncludeInMaterialRequest).HasColumnName("include_in_material_request").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");

        entity.HasIndex(x => new { x.ManufacturingLossProfileId, x.SequenceNo })
            .IsUnique().HasDatabaseName("ux_manufacturing_loss_profile_rules_profile_sequence");
        entity.HasIndex(x => x.ManufacturingLossTypeId).HasDatabaseName("ix_manufacturing_loss_profile_rules_type");
        entity.HasIndex(x => x.MaterialId).HasDatabaseName("ix_manufacturing_loss_profile_rules_material");
        entity.HasIndex(x => new { x.ManufacturingLossProfileId, x.IsActive })
            .HasDatabaseName("ix_manufacturing_loss_profile_rules_profile_active");

        entity.HasOne(x => x.Profile).WithMany(x => x.Rules).HasForeignKey(x => x.ManufacturingLossProfileId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_profile_rules_profile");
        entity.HasOne(x => x.LossType).WithMany(x => x.ProfileRules).HasForeignKey(x => x.ManufacturingLossTypeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_profile_rules_type");
        entity.HasOne(x => x.Material).WithMany(x => x.ManufacturingLossProfileRules).HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_manufacturing_loss_profile_rules_material");
    }
}
