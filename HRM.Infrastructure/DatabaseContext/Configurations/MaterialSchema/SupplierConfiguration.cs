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
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> entity)
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666B4029CD1B8");
            entity.ToTable("Suppliers", "Material");

            entity.HasIndex(e => e.CompanyId, "IX_Suppliers_CompanyId");
            entity.HasIndex(e => e.CreatedBy, "IX_Suppliers_CreatedBy");

            entity.Property(e => e.SupplierId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ExternalId).HasColumnType("citext");
            entity.Property(e => e.IssuedPlace).HasColumnType("citext");
            entity.Property(e => e.SupplierName).HasColumnType("citext");
            entity.Property(e => e.Note).HasColumnType("citext");
            entity.Property(e => e.Phone);
            entity.Property(e => e.RegistrationNumber).HasColumnType("citext");
            entity.Property(e => e.RegistrationAddress).HasColumnType("citext");
            entity.Property(e => e.Website).HasColumnType("citext");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Company).WithMany(p => p.Suppliers)
                .HasForeignKey(d => d.CompanyId)
                .HasConstraintName("FK_Suppliers_Company");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SupplierCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Suppliers_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SupplierUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_Supplier_UpdatedBy");
        }
    }
}
