using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingProcessTemplateConfiguration : IEntityTypeConfiguration<ManufacturingProcessTemplate>
{
    public void Configure(EntityTypeBuilder<ManufacturingProcessTemplate> entity)
    {
        entity.ToTable("manufacturing_process_templates", "bom", table => table.HasCheckConstraint("ck_manufacturing_process_templates_effective_range", "effective_to IS NULL OR effective_from IS NULL OR effective_to >= effective_from"));
        entity.HasKey(x => x.ManufacturingProcessTemplateId).HasName("pk_manufacturing_process_templates");
        entity.Property(x => x.ManufacturingProcessTemplateId).HasColumnName("manufacturing_process_template_id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        entity.Property(x => x.ExternalId).HasColumnName("external_id").HasColumnType("citext").HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.VersionNo).HasColumnName("version_no").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("citext").HasMaxLength(32).IsRequired();
        entity.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
        entity.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        entity.Property(x => x.CreatedDate).HasColumnName("created_date").IsRequired();
        entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(x => x.UpdatedDate).HasColumnName("updated_date");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        entity.Property(x => x.ReleasedDate).HasColumnName("released_date");
        entity.Property(x => x.ReleasedBy).HasColumnName("released_by");
        entity.HasIndex(x => new { x.CompanyId, x.ExternalId, x.VersionNo }).IsUnique().HasDatabaseName("ux_process_templates_company_external_version");
        entity.HasIndex(x => new { x.CompanyId, x.Status }).HasDatabaseName("ix_process_templates_company_status");
        entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_templates_company");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_templates_created_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_templates_updated_by");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReleasedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_process_templates_released_by");
    }
}
