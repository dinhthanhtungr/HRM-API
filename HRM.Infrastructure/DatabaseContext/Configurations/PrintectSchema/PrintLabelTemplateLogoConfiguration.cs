using HRM.Domain.Entities.PrintectSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema;

public sealed class PrintLabelTemplateLogoConfiguration : IEntityTypeConfiguration<PrintLabelTemplateLogo>
{
    public void Configure(EntityTypeBuilder<PrintLabelTemplateLogo> entity)
    {
        entity.ToTable("PrintLabelTemplateLogos", "printect");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.PrintLabelTemplateId).HasColumnName("printLabelTemplateId").IsRequired();
        entity.Property(x => x.PrintLabelLogoId).HasColumnName("printLabelLogoId").IsRequired();
        entity.Property(x => x.SortOrder).HasColumnName("sortOrder");
        entity.Property(x => x.IsDefault).HasColumnName("isDefault").HasDefaultValue(false);
        entity.Property(x => x.IsActive).HasColumnName("isActive").HasDefaultValue(true);
        entity.HasIndex(x => new { x.PrintLabelTemplateId, x.PrintLabelLogoId }).IsUnique().HasDatabaseName("UX_PrintLabelTemplateLogos_Template_Logo");
        entity.HasOne(x => x.Template).WithMany(x => x.TemplateLogos).HasForeignKey(x => x.PrintLabelTemplateId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PrintLabelTemplateLogos_Template");
        entity.HasOne(x => x.Logo).WithMany(x => x.TemplateLogos).HasForeignKey(x => x.PrintLabelLogoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PrintLabelTemplateLogos_Logo");
    }
}
