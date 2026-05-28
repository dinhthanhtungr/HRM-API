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
    public class ProductTestConfiguration : IEntityTypeConfiguration<ProductTest>
    {
        public void Configure(EntityTypeBuilder<ProductTest> entity)
        {
            entity.ToTable("ProductTest", "devandqa");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.ExternalId).HasColumnName("externalid").HasColumnType("citext");
            entity.Property(x => x.ProductExternalId).HasColumnName("product_externalid").HasColumnType("citext");
            entity.Property(x => x.ProductCustomerExternalId).HasColumnName("product_customerexternalid").HasColumnType("citext");

            entity.Property(x => x.ManufacturingDate).HasColumnName("manufacturingdate");
            entity.Property(x => x.ExpiryDate).HasColumnName("expirydate");

            entity.Property(x => x.ProductPackage).HasColumnName("product_package").HasColumnType("citext");
            entity.Property(x => x.ProductWeight).HasColumnName("product_weight");

            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.ProductName).HasColumnName("product_name").HasColumnType("citext");


            entity.HasIndex(x => x.ProductId).HasDatabaseName("ix_producttest_productid");
            entity.HasIndex(x => new { x.ProductExternalId, x.ManufacturingDate })
                  .HasDatabaseName("ix_producttest_externalid_mfgdate");
        }
    }

}
