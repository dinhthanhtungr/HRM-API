using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema
{
    public class ProductInspectionConfiguration : IEntityTypeConfiguration<ProductInspection>
    {
        public void Configure(EntityTypeBuilder<ProductInspection> entity)
        {
            entity.ToTable("ProductInspection", schema: "devandqa");

            // PK (uuid mặc định)
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            // Thông tin lô & SP
            entity.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("citext");
            entity.Property(x => x.ProductCode).HasColumnName("product_code").HasColumnType("citext");
            entity.Property(x => x.ProductName).HasColumnName("product_name").HasColumnType("citext");
            entity.Property(x => x.ProductStandardId).HasColumnName("product_standard_id");
            entity.Property(x => x.Weight).HasColumnName("weight");

            // Ngày tháng
            entity.Property(x => x.ManufacturingDate).HasColumnName("manufacturing_date");
            entity.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(x => x.CreateDate).HasColumnName("create_date");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("citext");

            // Thuộc tính hình dạng/mesh/particle/packing…
            entity.Property(x => x.Shape).HasColumnName("shape").HasColumnType("citext");
            entity.Property(x => x.IsShapePass).HasColumnName("is_shape_pass");

            entity.Property(x => x.ParticleSize).HasColumnName("particle_size").HasColumnType("citext");
            entity.Property(x => x.IsParticleSizePass).HasColumnName("is_particle_size_pass");

            entity.Property(x => x.PackingSpec).HasColumnName("packing_spec").HasColumnType("citext");
            entity.Property(x => x.IsPackingSpecPass).HasColumnName("is_packing_spec_pass");

            entity.Property(x => x.MeshType).HasColumnName("mesh_type").HasColumnType("citext");
            entity.Property(x => x.IsMeshAttached).HasColumnName("is_mesh_attached");

            // Visual & màu
            entity.Property(x => x.VisualCheck).HasColumnName("visual_check");
            entity.Property(x => x.ColorDeltaE).HasColumnName("color_delta_e").HasColumnType("citext");
            entity.Property(x => x.IsColorDeltaEpass).HasColumnName("is_color_delta_epass");

            // Các chỉ tiêu (để nguyên string theo entity bạn gửi)
            entity.Property(x => x.Moisture).HasColumnName("moisture").HasColumnType("citext");
            entity.Property(x => x.IsMoisturePass).HasColumnName("is_moisture_pass");

            entity.Property(x => x.Mfr).HasColumnName("mfr").HasColumnType("citext");
            entity.Property(x => x.IsMfrpass).HasColumnName("is_mfrpass");

            entity.Property(x => x.FlexuralStrength).HasColumnName("flexural_strength").HasColumnType("citext");
            entity.Property(x => x.IsFlexuralStrengthPass).HasColumnName("is_flexural_strength_pass");

            entity.Property(x => x.Elongation).HasColumnName("elongation").HasColumnType("citext");
            entity.Property(x => x.IsElongationPass).HasColumnName("is_elongation_pass");

            entity.Property(x => x.Hardness).HasColumnName("hardness").HasColumnType("citext");
            entity.Property(x => x.IsHardnessPass).HasColumnName("is_hardness_pass");

            entity.Property(x => x.Density).HasColumnName("density").HasColumnType("citext");
            entity.Property(x => x.IsDensityPass).HasColumnName("is_density_pass");

            entity.Property(x => x.TensileStrength).HasColumnName("tensile_strength").HasColumnType("citext");
            entity.Property(x => x.IsTensileStrengthPass).HasColumnName("is_tensile_strength_pass");

            entity.Property(x => x.FlexuralModulus).HasColumnName("flexural_modulus").HasColumnType("citext");
            entity.Property(x => x.IsFlexuralModulusPass).HasColumnName("is_flexural_modulus_pass");

            entity.Property(x => x.ImpactResistance).HasColumnName("impact_resistance").HasColumnType("citext");
            entity.Property(x => x.IsImpactResistancePass).HasColumnName("is_impact_resistance_pass");

            entity.Property(x => x.Antistatic).HasColumnName("antistatic").HasColumnType("citext");
            entity.Property(x => x.IsAntistaticPass).HasColumnName("is_antistatic_pass");

            entity.Property(x => x.StorageCondition).HasColumnName("storage_condition").HasColumnType("citext");
            entity.Property(x => x.IsStorageConditionPass).HasColumnName("is_storage_condition_pass");

            entity.Property(x => x.IntrinsicViscosity).HasColumnName("intrinsic_viscosity").HasColumnType("citext");
            entity.Property(x => x.IsIntrinsicViscosity).HasColumnName("is_intrinsic_viscosity");

            // Khác
            entity.Property(x => x.DwellTime).HasColumnName("dwell_time");             // bool?
            entity.Property(x => x.BlackDots).HasColumnName("black_dots").HasColumnType("citext");
            entity.Property(x => x.MigrationTest).HasColumnName("migration_test");

            // Lỗi/khuyết tật
            entity.Property(x => x.DefectImpurity).HasColumnName("defect_impurity");
            entity.Property(x => x.DefectBlackDot).HasColumnName("defect_black_dot");
            entity.Property(x => x.DefectShortFiber).HasColumnName("defect_short_fiber");
            entity.Property(x => x.DefectMoist).HasColumnName("defect_moist");
            entity.Property(x => x.DefectDusty).HasColumnName("defect_dusty");
            entity.Property(x => x.DefectWrongColor).HasColumnName("defect_wrong_color");

            // Kết luận & ghi chú
            entity.Property(x => x.DeliveryAccepted).HasColumnName("delivery_accepted");
            entity.Property(x => x.Notes).HasColumnName("notes"); // text

            // External
            entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext");
            entity.Property(x => x.Types).HasColumnName("types").HasColumnType("citext");

            // Index gợi ý (log/QC truy vấn theo ngày, mã, lô)
            entity.HasIndex(x => x.CreateDate).HasDatabaseName("ix_productinspection_create_date");
            entity.HasIndex(x => new { x.ProductCode, x.CreateDate })
                  .HasDatabaseName("ix_productinspection_product_create_date");
            entity.HasIndex(x => new { x.BatchId, x.CreateDate })
                  .HasDatabaseName("ix_productinspection_batch_create_date");
            entity.HasIndex(x => x.ExternalId)
                  .HasDatabaseName("ix_productinspection_external_id");
        }
    }
}
