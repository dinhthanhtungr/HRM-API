using HRM.Domain.Entities.PrintectSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema;

public sealed class PrintLabelElementConfiguration : IEntityTypeConfiguration<PrintLabelElement>
{
    public void Configure(EntityTypeBuilder<PrintLabelElement> entity)
    {
        entity.ToTable("PrintLabelElements", "printect");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        entity.Property(x => x.PrintLabelTemplateId).HasColumnName("printLabelTemplateId").IsRequired();
        entity.Property(x => x.LineNo).HasColumnName("lineNo").IsRequired();
        entity.Property(x => x.FieldKey).HasColumnName("fieldKey").HasColumnType("citext").HasMaxLength(100).IsRequired();
        entity.Property(x => x.DefaultValue).HasColumnName("defaultValue").HasColumnType("text");
        entity.Property(x => x.IsActive).HasColumnName("isActive").HasDefaultValue(true);
        entity.HasIndex(x => new { x.PrintLabelTemplateId, x.LineNo }).IsUnique().HasDatabaseName("UX_PrintLabelElements_Template_Line");
        entity.HasIndex(x => new { x.PrintLabelTemplateId, x.FieldKey }).IsUnique().HasDatabaseName("UX_PrintLabelElements_Template_Field");
        entity.HasOne(x => x.Template).WithMany(x => x.Elements).HasForeignKey(x => x.PrintLabelTemplateId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PrintLabelElements_Template");
    }
}
