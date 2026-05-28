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
    public class MaterialConfiguration : IEntityTypeConfiguration<Material>
    {
        public void Configure(EntityTypeBuilder<Material> entity)
        {
            entity.HasKey(e => e.MaterialId).HasName("PK__Material__C50610F7C355BA5C");
            entity.ToTable("Materials", "Material");

            entity.HasIndex(e => e.CategoryId, "IX_Materials_CategoryId");
            entity.HasIndex(e => e.CompanyId, "IX_Materials_CompanyId");
            entity.HasIndex(e => e.CreatedBy, "IX_Materials_CreatedBy");
            entity.HasIndex(e => e.UpdatedBy, "IX_Materials_UpdatedBy");
            // entity.HasIndex(e => e.UnitId, "IX_Materials_UnitId"); // nếu dùng sau này

            entity.Property(e => e.MaterialId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Barcode).HasColumnType("citext");
            entity.Property(e => e.Comment).HasColumnType("citext");
            entity.Property(e => e.CustomCode).HasColumnType("citext");
            entity.Property(e => e.ExternalId).HasColumnType("citext");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasColumnType("citext");
            entity.Property(e => e.Package).HasColumnType("citext");

            entity.HasOne(d => d.Category).WithMany(p => p.Materials)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Materials_Category");

            entity.HasOne(d => d.Company).WithMany(p => p.Materials)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Materials_Company");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaterialCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Materials_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaterialUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_Materials_UpdatedBy");
        }
    }
}
