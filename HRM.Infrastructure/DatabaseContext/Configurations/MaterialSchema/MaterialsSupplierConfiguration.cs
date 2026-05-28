using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.MaterialSchema
{
    public class MaterialsSupplierConfiguration : IEntityTypeConfiguration<MaterialsSupplier>
    {
        public void Configure(EntityTypeBuilder<MaterialsSupplier> entity)
        {
            entity.HasKey(e => e.MaterialsSuppliersId).HasName("PK__Material__4F13EDBB73A34869");
            entity.ToTable("Materials_Suppliers", "Material");

            entity.HasIndex(e => e.MaterialId, "IX_Materials_Suppliers_MaterialId");
            entity.HasIndex(e => e.SupplierId, "IX_Materials_Suppliers_SupplierId");
            entity.HasIndex(e => e.UpdatedBy, "IX_Materials_Suppliers_UpdatedBy");

            entity.Property(e => e.MaterialsSuppliersId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("Materials_SuppliersId");
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.CurrentPrice).HasPrecision(18, 4);
            entity.Property(e => e.IsPreferred).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Material).WithMany(p => p.MaterialsSuppliers)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MaterialsSuppliers_Material");

            entity.HasOne(d => d.Supplier).WithMany(p => p.MaterialsSuppliers)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MaterialsSuppliers_Supplier");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaterialsSupplierCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_MaterialsSuppliers_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaterialsSupplierUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_MaterialsSuppliers_UpdatedBy");

            // Partial unique index: chỉ 1 supplier "preferred" cho mỗi material
            entity.HasIndex(x => new { x.MaterialId, x.IsPreferred })
                  .HasFilter("\"IsPreferred\" = TRUE")
                  .IsUnique();
        }
    }
}
