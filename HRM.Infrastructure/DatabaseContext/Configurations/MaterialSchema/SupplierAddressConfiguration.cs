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
    public class SupplierAddressConfiguration : IEntityTypeConfiguration<SupplierAddress>
    {
        public void Configure(EntityTypeBuilder<SupplierAddress> entity)
        {
            entity.HasKey(e => e.AddressId).HasName("PK__Supplier__091C2AFB0B8862E7");
            entity.ToTable("SupplierAddresses", "Material");

            entity.HasIndex(e => e.SupplierId, "IX_SupplierAddresses_SupplierId");

            entity.Property(e => e.AddressId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AddressLine).HasColumnType("citext");
            entity.Property(e => e.City).HasColumnType("citext");
            entity.Property(e => e.Country).HasColumnType("citext");
            entity.Property(e => e.District).HasColumnType("citext");
            entity.Property(e => e.IsPrimary).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PostalCode).HasColumnType("citext");
            entity.Property(e => e.Province).HasColumnType("citext");

            entity.HasOne(d => d.Supplier)
                  .WithMany(p => p.SupplierAddresses)
                  .HasForeignKey(d => d.SupplierId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_SupplierAddress_Supplier");
        }
    }
}
