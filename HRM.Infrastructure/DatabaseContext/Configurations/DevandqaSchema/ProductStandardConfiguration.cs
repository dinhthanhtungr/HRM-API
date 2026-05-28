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
    public class ProductStandardConfiguration : IEntityTypeConfiguration<ProductStandard>
    {
        public void Configure(EntityTypeBuilder<ProductStandard> entity)
        {
            entity.ToTable("ProductStandard", "devandqa");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.ProductExternalId).HasColumnName("productexternalid").HasColumnType("citext");
            entity.Property(x => x.externalId).HasColumnName("externalid").HasColumnType("citext");
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("citext");

            entity.Property(x => x.DeltaE).HasColumnName("deltae").HasColumnType("citext");
            entity.Property(x => x.PelletSize).HasColumnName("pelletsize").HasColumnType("citext");
            entity.Property(x => x.Moisture).HasColumnName("moisture").HasColumnType("citext");
            entity.Property(x => x.Density).HasColumnName("density").HasColumnType("citext");
            entity.Property(x => x.MeltIndex).HasColumnName("meltindex").HasColumnType("citext");
            entity.Property(x => x.TensileStrength).HasColumnName("tensilestrength").HasColumnType("citext");
            entity.Property(x => x.ElongationAtBreak).HasColumnName("elongationatbreak").HasColumnType("citext");
            entity.Property(x => x.FlexuralStrength).HasColumnName("flexuralstrength").HasColumnType("citext");
            entity.Property(x => x.FlexuralModulus).HasColumnName("flexuralmodulus").HasColumnType("citext");
            entity.Property(x => x.IzodImpactStrength).HasColumnName("izodimpactstrength").HasColumnType("citext");
            entity.Property(x => x.Hardness).HasColumnName("hardness").HasColumnType("citext");
            entity.Property(x => x.DwellTime).HasColumnName("dwelltime").HasColumnType("citext");
            entity.Property(x => x.BlackDots).HasColumnName("blackdots").HasColumnType("citext");
            entity.Property(x => x.MigrationTest).HasColumnName("migrationtest").HasColumnType("citext");

            entity.Property(x => x.CompanyId).HasColumnName("companyid");
            entity.Property(x => x.CreatedDate).HasColumnName("createddate");

            entity.Property(x => x.ProductId).HasColumnName("productid");
            entity.Property(x => x.Weight).HasColumnName("weight");
            entity.Property(x => x.Shape).HasColumnName("shape").HasColumnType("citext");
            entity.Property(x => x.packed).HasColumnName("packed").HasColumnType("citext");

            entity.HasOne(d => d.Product).WithMany(d => d.ProductStandards)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductStandard_Product");

            entity.HasOne(d => d.Company).WithMany()
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductStandard_Company");

            entity.HasOne(d => d.CreatedByNavigation).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_ProductStandard_CreatedBy");


            // Gợi ý index
            entity.HasIndex(x => x.ProductId).HasDatabaseName("ix_productstandard_productid");
            entity.HasIndex(x => x.externalId).HasDatabaseName("ix_productstandard_externalid");
            entity.HasIndex(x => x.ProductExternalId).HasDatabaseName("ix_productstandard_productexternalid");
            entity.HasIndex(x => x.CreatedDate).HasDatabaseName("ix_productstandard_createddate");
        }
    }
}
