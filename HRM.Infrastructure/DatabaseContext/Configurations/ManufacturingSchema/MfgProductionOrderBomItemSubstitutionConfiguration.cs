using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema;

public sealed class MfgProductionOrderBomItemSubstitutionConfiguration : IEntityTypeConfiguration<MfgProductionOrderBomItemSubstitution>
{
    public void Configure(EntityTypeBuilder<MfgProductionOrderBomItemSubstitution> entity)
    {
        entity.ToTable("mfg_production_order_bom_item_substitutions", "manufacturing", table =>
        {
            table.HasCheckConstraint("ck_mfg_production_order_bom_item_substitutions_planned_positive", "planned_quantity > 0");
            table.HasCheckConstraint("ck_mfg_production_order_bom_item_substitutions_actual_positive", "actual_quantity > 0");
            table.HasCheckConstraint("ck_mfg_production_order_bom_item_substitutions_ratio_positive", "applied_ratio > 0");
            table.HasCheckConstraint(
                "ck_mfg_production_order_bom_item_substitutions_actual_target",
                "(actual_item_type = 'Material' AND actual_material_id IS NOT NULL AND actual_component_product_id IS NULL) OR " +
                "(actual_item_type = 'Product' AND actual_component_product_id IS NOT NULL AND actual_material_id IS NULL)");
        });
        entity.HasKey(x => x.MfgProductionOrderBomItemSubstitutionId).HasName("pk_mfg_production_order_bom_item_substitutions");

        entity.Property(x => x.MfgProductionOrderBomItemSubstitutionId).HasColumnName("mfg_production_order_bom_item_substitution_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        entity.Property(x => x.MfgProductionOrderId).HasColumnName("mfg_production_order_id").IsRequired();
        entity.Property(x => x.BomVersionItemId).HasColumnName("bom_version_item_id").IsRequired();
        entity.Property(x => x.BomVersionItemAlternativeId).HasColumnName("bom_version_item_alternative_id");
        entity.Property(x => x.SourceItemTypeSnapshot).HasColumnName("source_item_type_snapshot").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.SourceItemCodeSnapshot).HasColumnName("source_item_code_snapshot").HasColumnType("citext").HasMaxLength(100);
        entity.Property(x => x.SourceItemNameSnapshot).HasColumnName("source_item_name_snapshot").HasMaxLength(300);
        entity.Property(x => x.ActualItemType).HasColumnName("actual_item_type").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.ActualMaterialId).HasColumnName("actual_material_id");
        entity.Property(x => x.ActualComponentProductId).HasColumnName("actual_component_product_id");
        entity.Property(x => x.PlannedQuantity).HasColumnName("planned_quantity").HasPrecision(18, 3).IsRequired();
        entity.Property(x => x.ActualQuantity).HasColumnName("actual_quantity").HasPrecision(18, 3).IsRequired();
        entity.Property(x => x.AppliedRatio).HasColumnName("applied_ratio").HasPrecision(18, 6).IsRequired();
        entity.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text").IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.RequestedDate).HasColumnName("requested_date").IsRequired();
        entity.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        entity.Property(x => x.ApprovedDate).HasColumnName("approved_date");
        entity.Property(x => x.ApprovedBy).HasColumnName("approved_by");
        entity.Property(x => x.AppliedDate).HasColumnName("applied_date");
        entity.Property(x => x.AppliedBy).HasColumnName("applied_by");
        entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        entity.HasIndex(x => new { x.CompanyId, x.MfgProductionOrderId, x.Status })
            .HasDatabaseName("ix_mfg_production_order_bom_item_substitutions_company_order_status");
        entity.HasIndex(x => new { x.MfgProductionOrderId, x.BomVersionItemId })
            .HasDatabaseName("ix_mfg_production_order_bom_item_substitutions_order_item");
        entity.HasIndex(x => x.BomVersionItemAlternativeId)
            .HasDatabaseName("ix_mfg_production_order_bom_item_substitutions_alternative");

        entity.HasOne(x => x.Company).WithMany(x => x.MfgProductionOrderBomItemSubstitutions).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_company");
        entity.HasOne(x => x.ProductionOrder).WithMany(x => x.BomItemSubstitutions).HasForeignKey(x => x.MfgProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_order");
        entity.HasOne(x => x.BomVersionItem).WithMany(x => x.ProductionOrderSubstitutions).HasForeignKey(x => x.BomVersionItemId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_item");
        entity.HasOne(x => x.BomVersionItemAlternative).WithMany(x => x.ProductionOrderSubstitutions).HasForeignKey(x => x.BomVersionItemAlternativeId)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_alternative");
        entity.HasOne(x => x.ActualMaterial).WithMany(x => x.ProductionOrderBomItemSubstitutions).HasForeignKey(x => x.ActualMaterialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_material");
        entity.HasOne(x => x.ActualComponentProduct).WithMany(x => x.ProductionOrderBomItemSubstitutions).HasForeignKey(x => x.ActualComponentProductId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_product");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_requested_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_approved_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.AppliedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_applied_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfg_production_order_bom_item_substitutions_updated_by");
    }
}
