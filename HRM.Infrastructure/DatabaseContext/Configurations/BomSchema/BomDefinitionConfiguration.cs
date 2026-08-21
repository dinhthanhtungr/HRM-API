using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public class BomDefinitionConfiguration : IEntityTypeConfiguration<BomDefinition>
{
    public void Configure(EntityTypeBuilder<BomDefinition> entity)
    {
        entity.ToTable("bom_definitions", "bom");
        entity.HasKey(x => x.BomDefinitionId).HasName("pk_bom_definitions");

        entity.Property(x => x.BomDefinitionId).HasColumnName("bom_definition_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        entity.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        entity.Property(x => x.Code).HasColumnName("code").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.BomType).HasColumnName("bom_type").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.Property(x => x.CreatedDate).HasColumnName("created_date").IsRequired();
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        entity.HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique().HasDatabaseName("ux_bom_definitions_company_code");
        entity.HasIndex(x => new { x.CompanyId, x.ProductId, x.BomType, x.IsActive })
            .HasDatabaseName("ix_bom_definitions_company_product_type_active");

        entity.HasOne(x => x.Product).WithMany(x => x.BomDefinitions).HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_definitions_product");
        entity.HasOne(x => x.Company).WithMany(x => x.BomDefinitions).HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_definitions_company");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_definitions_created_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_bom_definitions_updated_by");
    }
}
