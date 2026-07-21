using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema
{
    public class AddressConfiguration : IEntityTypeConfiguration<Address>
    {
        public void Configure(EntityTypeBuilder<Address> entity)
        {
            entity.HasKey(e => e.AddressId).HasName("PK__Address__091C2A1BCCA306B0");
            entity.ToTable("Address", "Customer");

            entity.Property(e => e.AddressId)
                  .HasColumnName("AddressId")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.AddressLine).HasColumnName("AddressLine").HasColumnType("citext");
            entity.Property(e => e.City).HasColumnName("City").HasColumnType("citext");
            entity.Property(e => e.Country).HasColumnName("Country").HasColumnType("citext");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerId");
            entity.Property(e => e.District).HasColumnName("District").HasColumnType("citext");
            entity.Property(e => e.IsPrimary).HasColumnName("IsPrimary").HasDefaultValue(false);
            entity.Property(e => e.PostalCode).HasColumnName("PostalCode").HasColumnType("citext");
            entity.Property(e => e.Province).HasColumnName("Province").HasColumnType("citext");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_Address_CustomerId");
            entity.HasIndex(e => new { e.CustomerId, e.IsPrimary }).HasDatabaseName("IX_Address_Customer_IsPrimary");

            // XÓA CUSTOMER -> XÓA ADDRESS
            entity.HasOne(d => d.Customer)
                  .WithMany(p => p.Addresses)
                  .HasForeignKey(d => d.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_Address_Customer");
        }
    }
}
