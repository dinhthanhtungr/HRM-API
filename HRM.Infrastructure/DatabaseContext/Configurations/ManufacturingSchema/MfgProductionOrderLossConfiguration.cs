using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema;

public class MfgProductionOrderLossConfiguration : IEntityTypeConfiguration<MfgProductionOrderLoss>
{
    public void Configure(EntityTypeBuilder<MfgProductionOrderLoss> entity)
    {
        entity.ToTable("mfg_production_order_losses", "manufacturing", table =>
        {
            table.HasCheckConstraint("ck_mfg_production_order_losses_planned_nonnegative", "planned_quantity_kg >= 0");
            table.HasCheckConstraint("ck_mfg_production_order_losses_actual_nonnegative", "actual_quantity_kg IS NULL OR actual_quantity_kg >= 0");
            table.HasCheckConstraint("ck_mfg_production_order_losses_event_count_nonnegative", "event_count IS NULL OR event_count >= 0");
            table.HasCheckConstraint("ck_mfg_production_order_losses_recovered_nonnegative", "recovered_quantity_kg >= 0");
        });
        entity.HasKey(x => x.MfgProductionOrderLossId).HasName("pk_mfg_production_order_losses");

        entity.Property(x => x.MfgProductionOrderLossId).HasColumnName("mfg_production_order_loss_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        entity.Property(x => x.MfgProductionOrderId).HasColumnName("mfg_production_order_id").IsRequired();
        entity.Property(x => x.SourceManufacturingBomLossRuleId).HasColumnName("source_manufacturing_bom_loss_rule_id");
        entity.Property(x => x.LossTypeCodeSnapshot).HasColumnName("loss_type_code_snapshot").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.LossTypeNameSnapshot).HasColumnName("loss_type_name_snapshot").HasMaxLength(200).IsRequired();
        entity.Property(x => x.CalculationMethodSnapshot).HasColumnName("calculation_method_snapshot")
            .HasConversion<string>().HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.StageCodeSnapshot).HasColumnName("stage_code_snapshot").HasColumnType("citext").HasMaxLength(64);
        entity.Property(x => x.MaterialCodeSnapshot).HasColumnName("material_code_snapshot").HasColumnType("citext").HasMaxLength(100);
        entity.Property(x => x.RatePercentSnapshot).HasColumnName("rate_percent_snapshot").HasPrecision(9, 6);
        entity.Property(x => x.FixedQuantityKgSnapshot).HasColumnName("fixed_quantity_kg_snapshot").HasPrecision(18, 3);
        entity.Property(x => x.QuantityPerEventKgSnapshot).HasColumnName("quantity_per_event_kg_snapshot").HasPrecision(18, 3);
        entity.Property(x => x.PlannedQuantityKg).HasColumnName("planned_quantity_kg").HasPrecision(18, 3).HasDefaultValue(0m).IsRequired();
        entity.Property(x => x.ActualQuantityKg).HasColumnName("actual_quantity_kg").HasPrecision(18, 3);
        entity.Property(x => x.EventCount).HasColumnName("event_count");
        entity.Property(x => x.RecoveredQuantityKg).HasColumnName("recovered_quantity_kg").HasPrecision(18, 3).HasDefaultValue(0m).IsRequired();
        entity.Property(x => x.IsFinalized).HasColumnName("is_finalized").HasDefaultValue(false).IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.RecordedDate).HasColumnName("recorded_date").IsRequired();
        entity.Property(x => x.RecordedBy).HasColumnName("recorded_by").IsRequired();
        entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        entity.HasIndex(x => new { x.CompanyId, x.MfgProductionOrderId })
            .HasDatabaseName("ix_mfg_production_order_losses_company_order");
        entity.HasIndex(x => new { x.MfgProductionOrderId, x.IsFinalized })
            .HasDatabaseName("ix_mfg_production_order_losses_order_finalized");
        entity.HasIndex(x => x.SourceManufacturingBomLossRuleId)
            .HasDatabaseName("ix_mfg_production_order_losses_source_rule");
        entity.HasIndex(x => x.LossTypeCodeSnapshot)
            .HasDatabaseName("ix_mfg_production_order_losses_type_code");

        entity.HasOne<Company>().WithMany(x => x.MfgProductionOrderLosses).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_losses_company");
        entity.HasOne(x => x.ProductionOrder).WithMany(x => x.Losses).HasForeignKey(x => x.MfgProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_losses_order");
        entity.HasOne(x => x.SourceLossRule).WithMany(x => x.ProductionOrderLosses).HasForeignKey(x => x.SourceManufacturingBomLossRuleId)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_mfg_production_order_losses_source_rule");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_losses_recorded_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_losses_updated_by");
    }
}
