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
        entity.Property(x => x.ElementType).HasColumnName("elementType").HasColumnType("citext").HasMaxLength(30).IsRequired();
        entity.Property(x => x.FieldKey).HasColumnName("fieldKey").HasColumnType("citext").HasMaxLength(100);
        entity.Property(x => x.DisplayName).HasColumnName("displayName").HasMaxLength(200);
        entity.Property(x => x.ValueSource).HasColumnName("valueSource").HasColumnType("citext").HasMaxLength(30);
        entity.Property(x => x.DefaultValue).HasColumnName("defaultValue").HasColumnType("text");
        entity.Property(x => x.PrefixText).HasColumnName("prefixText").HasMaxLength(200);
        entity.Property(x => x.IsRequired).HasColumnName("isRequired").HasDefaultValue(false);
        entity.Property(x => x.IsEditableBySales).HasColumnName("isEditableBySales").HasDefaultValue(false);
        entity.Property(x => x.IsActive).HasColumnName("isActive").HasDefaultValue(true);
        entity.Property(x => x.X).HasColumnName("x");
        entity.Property(x => x.Y).HasColumnName("y");
        entity.Property(x => x.Width).HasColumnName("width");
        entity.Property(x => x.Height).HasColumnName("height");
        entity.Property(x => x.FontName).HasColumnName("fontName").HasMaxLength(100);
        entity.Property(x => x.FontSize).HasColumnName("fontSize").HasPrecision(8, 2);
        entity.Property(x => x.Alignment).HasColumnName("alignment").HasColumnType("citext").HasMaxLength(20);
        entity.Property(x => x.Bold).HasColumnName("bold").HasDefaultValue(false);
        entity.Property(x => x.Italic).HasColumnName("italic").HasDefaultValue(false);
        entity.HasIndex(x => new { x.PrintLabelTemplateId, x.LineNo }).IsUnique().HasDatabaseName("UX_PrintLabelElements_Template_Line");
        entity.HasIndex(x => new { x.PrintLabelTemplateId, x.FieldKey }).IsUnique().HasFilter("\"fieldKey\" IS NOT NULL").HasDatabaseName("UX_PrintLabelElements_Template_Field");
        entity.HasOne(x => x.Template).WithMany(x => x.Elements).HasForeignKey(x => x.PrintLabelTemplateId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PrintLabelElements_Template");
    }
}
