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
    public class SupplierContactConfiguration : IEntityTypeConfiguration<SupplierContact>
    {
        public void Configure(EntityTypeBuilder<SupplierContact> entity)
        {
            entity.HasKey(e => e.ContactId).HasName("PK__Supplier__5C66259B7A3571DD");
            entity.ToTable("SupplierContacts", "Material");

            entity.HasIndex(e => e.SupplierId, "IX_SupplierContacts_SupplierId");

            entity.Property(e => e.ContactId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Email).HasColumnType("citext");
            entity.Property(e => e.FirstName).HasColumnType("citext");
            entity.Property(e => e.Gender).HasColumnType("citext");
            entity.Property(e => e.LastName).HasColumnType("citext");
            entity.Property(e => e.Phone).HasColumnType("citext");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Supplier)
                  .WithMany(p => p.SupplierContacts)
                  .HasForeignKey(d => d.SupplierId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_SupplierContact_Supplier");
        }
    }
}
