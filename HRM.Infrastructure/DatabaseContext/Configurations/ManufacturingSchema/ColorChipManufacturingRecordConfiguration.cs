using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class ColorChipManufacturingRecordConfiguration : IEntityTypeConfiguration<ColorChipManufacturingRecord>
    {
        public void Configure(EntityTypeBuilder<ColorChipManufacturingRecord> b)
        {
            b.ToTable("color_chip_manufacturing_records", "manufacturing");

            b.HasKey(x => x.ColorChipMfgRecordId);
            b.Property(x => x.ColorChipMfgRecordId)
                .HasColumnName("color_chip_mfg_record_id")
                .HasDefaultValueSql("gen_random_uuid()");

            // =========================================================
            // 1. Classification / Business Type
            // =========================================================
            b.Property(x => x.ResinType)
                .HasColumnName("resin_type")
                .HasConversion<int>()
                .IsRequired();
            b.Property(x => x.LogoType)
                .HasColumnName("logo_type")
                .HasConversion<int>()
                .IsRequired();
            b.Property(x => x.FormStyle)
                .HasColumnName("form_style")
                .HasConversion<int>()
                .IsRequired();

            // =========================================================
            // 3.MfgProductionOrder Snapshot
            // =========================================================
            b.Property(x => x.MfgProductionOrderId)
                .HasColumnName("mfg_production_order_id");
            // =========================================================
            // 4.ManufacturingFormula Snapshot
            // =========================================================
            b.Property(x => x.ManufacturingFormulaId)
                .HasColumnName("manufacturing_formula_id");

            // =========================================================
            // 5. Manufacturing Info
            // =========================================================
            b.Property(x => x.StandardFormula)
                .HasColumnName("standard_formula")
                .HasMaxLength(255);

            b.Property(x => x.Machine)
                .HasColumnName("machine")
                .HasMaxLength(255);

            b.Property(x => x.Resin)
                .HasColumnName("resin")
                .HasMaxLength(255);

            b.Property(x => x.TemperatureLimit)
                .HasColumnName("temperature_limit")
                .HasMaxLength(255);

            // =========================================================
            // 6. Size & Weight
            // =========================================================
            b.Property(x => x.SizeText)
                .HasColumnName("size_text")
                .HasMaxLength(255);

            b.Property(x => x.PelletWeightGram)
                .HasColumnName("pellet_weight_gram")
                .HasPrecision(18, 6);
            
            b.Property(x => x.NetWeightGram)
                .HasColumnName("net_weight_gram")
                .HasMaxLength(255);
            
            b.Property(x => x.Electrostatic)
                .HasColumnName("electrostatic");

            b.Property(x => x.DeltaE)
                .HasColumnName("delta_e")
                .HasMaxLength(255);

            // =========================================================
            // 7. Document / Record Info
            // =========================================================

            b.Property(x => x.RecordDate)
                .HasColumnName("record_date");

            b.Property(x => x.Note)
                .HasColumnName("note")
                .HasMaxLength(2000);

            b.Property(x => x.PrintNote)
                .HasColumnName("print_note")
                .HasMaxLength(2000);

            // =========================================================
            // 8. Audit
            // =========================================================
            b.Property(x => x.CreatedDate)
                .HasColumnName("created_date")
                .IsRequired();

            b.Property(x => x.CreatedBy)
                .HasColumnName("created_by")
                .IsRequired();

            b.Property(x => x.UpdatedDate)
                .HasColumnName("updated_date");

            b.Property(x => x.UpdatedBy)
                .HasColumnName("updated_by");

            b.Property(x => x.CompanyId)
                .HasColumnName("company_id")
                .IsRequired();

            b.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true)
                .IsRequired();


            // =========================================================
            // 9. Relationships
            // =========================================================

            b.HasOne(x => x.MfgProductionOrder)
                .WithMany()
                .HasForeignKey(x => x.MfgProductionOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.ManufacturingFormula)
                .WithMany()
                .HasForeignKey(x => x.ManufacturingFormulaId)
                .OnDelete(DeleteBehavior.Restrict);

            // =========================================================
            // 10. Indexes
            // =========================================================
            b.HasIndex(x => new { x.ColorChipMfgRecordId, x.IsActive})
                .HasDatabaseName("ix_color_chip_records_color_chip_mfg_record_id_is_active");

            b.HasIndex(x => new { x.RecordDate, x.IsActive })
                .HasDatabaseName("ix_color_chip_records_record_date_is_active");

        }
    }
}
