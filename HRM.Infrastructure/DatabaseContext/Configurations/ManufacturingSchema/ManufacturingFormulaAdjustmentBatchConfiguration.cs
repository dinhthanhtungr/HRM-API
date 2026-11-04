using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ManufacturingFormulaAdjustmentBatchConfiguration : IEntityTypeConfiguration<ManufacturingFormulaAdjustmentBatch>
    {
        public void Configure(EntityTypeBuilder<ManufacturingFormulaAdjustmentBatch> entity)
        {
            entity.ToTable("ManufacturingFormulaAdjustmentBatches", "manufacturing");

            entity.HasKey(e => e.ManufacturingFormulaAdjustmentBatchId)
                  .HasName("PK__ManufacturingFormulaAdjustmentBatches__manufacturingFormulaAdjustmentBatchId");

            entity.Property(e => e.ManufacturingFormulaAdjustmentBatchId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("manufacturingFormulaAdjustmentBatchId");

            entity.Property(e => e.ManufacturingFormulaAdjustmentId).HasColumnName("manufacturing_formula_adjustment_id");
            entity.Property(e => e.BatchNo).HasColumnName("batch_no");
            entity.Property(e => e.TrialNo).HasColumnName("trial_no");
            entity.Property(e => e.Status).HasColumnName("status").HasColumnType("citext").HasDefaultValue("Draft").IsRequired();
            entity.Property(e => e.AdjustmentType)
                  .HasColumnName("adjustment_type")
                  .HasColumnType("integer")
                  .IsRequired();
            entity.Property(e => e.IsAdditionalBatch).HasColumnName("is_additional_batch").HasDefaultValue(false).IsRequired();
            entity.Property(e => e.BatchQuantity).HasColumnName("batch_quantity").HasPrecision(16, 4);
            entity.Property(e => e.TakenQuantityGram).HasColumnName("taken_quantity_gram").HasPrecision(16, 4);
            entity.Property(e => e.FinalQuantityGram).HasColumnName("final_quantity_gram").HasPrecision(16, 4);
            entity.Property(e => e.ApplyToNextBatches).HasColumnName("apply_to_next_batches").HasDefaultValue(false).IsRequired();
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            entity.Property(e => e.CreatedDate).HasColumnName("created_date");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedDate).HasColumnName("updated_date");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasIndex(e => e.ManufacturingFormulaAdjustmentId)
                  .HasDatabaseName("ix_mfg_formula_adjustment_batches_adjustment_id");

            entity.HasIndex(e => new { e.ManufacturingFormulaAdjustmentId, e.BatchNo, e.TrialNo })
                  .IsUnique()
                  .HasFilter("is_active = true")
                  .HasDatabaseName("ux_mfg_formula_adjustment_batches_adjustment_batch_trial");

            entity.HasOne(e => e.ManufacturingFormulaAdjustment)
                  .WithMany(e => e.Batches)
                  .HasForeignKey(e => e.ManufacturingFormulaAdjustmentId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK__Mfab__manufacturingFormulaAdjustmentId");

            entity.HasOne(e => e.CreatedByNavigation)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfab__createdBy");

            entity.HasOne(e => e.UpdatedByNavigation)
                  .WithMany()
                  .HasForeignKey(e => e.UpdatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK__Mfab__updatedBy");
        }
    }
}
