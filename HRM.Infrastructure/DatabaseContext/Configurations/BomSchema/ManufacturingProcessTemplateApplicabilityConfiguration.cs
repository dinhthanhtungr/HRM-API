using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.BomSchema;

public sealed class ManufacturingProcessTemplateApplicabilityConfiguration
    : IEntityTypeConfiguration<ManufacturingProcessTemplateApplicability>
{
    public void Configure(EntityTypeBuilder<ManufacturingProcessTemplateApplicability> entity)
    {
        entity.ToTable("manufacturing_process_template_applicabilities", "bom", table =>
        {
            table.HasCheckConstraint(
                "ck_process_template_applicability_condition_required",
                "category_id IS NOT NULL OR step_of_product IS NOT NULL");
            table.HasCheckConstraint(
                "ck_process_template_applicability_priority_non_negative",
                "priority >= 0");
        });

        entity.HasKey(x => x.ManufacturingProcessTemplateApplicabilityId)
            .HasName("pk_process_template_applicabilities");
        entity.Property(x => x.ManufacturingProcessTemplateApplicabilityId)
            .HasColumnName("manufacturing_process_template_applicability_id")
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.ManufacturingProcessTemplateId)
            .HasColumnName("manufacturing_process_template_id")
            .IsRequired();
        entity.Property(x => x.CategoryId).HasColumnName("category_id");
        entity.Property(x => x.StepOfProduct).HasColumnName("step_of_product");
        entity.Property(x => x.Priority).HasColumnName("priority").HasDefaultValue(0).IsRequired();
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");

        entity.HasIndex(x => x.ManufacturingProcessTemplateId)
            .HasDatabaseName("ix_process_template_applicabilities_template");
        entity.HasIndex(x => new { x.CategoryId, x.StepOfProduct })
            .HasDatabaseName("ix_process_template_applicabilities_match");

        entity.HasOne(x => x.ProcessTemplate)
            .WithMany(x => x.ApplicabilityRules)
            .HasForeignKey(x => x.ManufacturingProcessTemplateId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_process_template_applicabilities_template");
        entity.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_process_template_applicabilities_category");
    }
}
