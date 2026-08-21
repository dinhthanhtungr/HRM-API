using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class ProductStandardBomVersionConfiguration : IEntityTypeConfiguration<ProductStandardBomVersion>
{
    public void Configure(EntityTypeBuilder<ProductStandardBomVersion> entity)
    {
        entity.ToTable("product_standard_bom_versions", "bom", table =>
            table.HasCheckConstraint("ck_product_standard_bom_versions_period", "valid_to IS NULL OR valid_to > valid_from"));
        entity.HasKey(x => x.ProductStandardBomVersionId).HasName("pk_product_standard_bom_versions");

        entity.Property(x => x.ProductStandardBomVersionId).HasColumnName("product_standard_bom_version_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        entity.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        entity.Property(x => x.BomVersionId).HasColumnName("bom_version_id").IsRequired();
        entity.Property(x => x.ValidFrom).HasColumnName("valid_from").IsRequired();
        entity.Property(x => x.ValidTo).HasColumnName("valid_to");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.CreatedDate).HasColumnName("created_date").IsRequired();
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.ClosedDate).HasColumnName("closed_date");
        entity.Property(x => x.ClosedBy).HasColumnName("closed_by");

        entity.HasIndex(x => new { x.CompanyId, x.ProductId })
            .HasDatabaseName("ix_product_standard_bom_versions_company_product");
        entity.HasIndex(x => x.BomVersionId).HasDatabaseName("ix_product_standard_bom_versions_version");
        entity.HasIndex(x => new { x.CompanyId, x.ProductId })
            .IsUnique().HasFilter("\"valid_to\" IS NULL")
            .HasDatabaseName("ux_product_standard_bom_versions_current");

        entity.HasOne(x => x.Product).WithMany(x => x.StandardBomVersions).HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_product_standard_bom_versions_product");
        entity.HasOne(x => x.BomVersion).WithMany(x => x.StandardProductAssignments).HasForeignKey(x => x.BomVersionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_product_standard_bom_versions_version");
        entity.HasOne(x => x.Company).WithMany(x => x.ProductStandardBomVersions).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_product_standard_bom_versions_company");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_product_standard_bom_versions_created_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ClosedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_product_standard_bom_versions_closed_by");
    }
}
