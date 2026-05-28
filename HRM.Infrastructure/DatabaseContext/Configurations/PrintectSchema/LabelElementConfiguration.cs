using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.PrintectSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema
{
    public class LabelElementConfiguration : IEntityTypeConfiguration<LabelElement>
    {
        public void Configure(EntityTypeBuilder<LabelElement> entity)
        {
            entity.ToTable("labelelements", "printect");
            entity.HasNoKey();
            entity.Property(x => x.ElementId).HasColumnName("elementid").UseIdentityAlwaysColumn();

            entity.Property(x => x.TemplateId).HasColumnName("templateid");
            entity.Property(x => x.LabelType).HasColumnName("labeltype").HasColumnType("citext");
            entity.Property(x => x.X).HasColumnName("x");
            entity.Property(x => x.Y).HasColumnName("y");
            entity.Property(x => x.Width).HasColumnName("width");
            entity.Property(x => x.Height).HasColumnName("height");
            entity.Property(x => x.FontSize).HasColumnName("fontsize");
            entity.Property(x => x.Alignment).HasColumnName("alignment").HasColumnType("citext");
            entity.Property(x => x.Bold).HasColumnName("bold");
            entity.Property(x => x.Italic).HasColumnName("italic");
            entity.Property(x => x.ValueType).HasColumnName("valuetype").HasColumnType("citext");
            entity.Property(x => x.PrefixText).HasColumnName("prefixtext");
            entity.Property(x => x.RenderType).HasColumnName("rendertype").HasColumnType("citext");
            entity.Property(x => x.FontName).HasColumnName("fontname").HasColumnType("citext");

            entity.HasIndex(x => x.TemplateId).HasDatabaseName("ix_labelelements_templateid");
            entity.HasIndex(x => new { x.TemplateId, x.LabelType })
                  .HasDatabaseName("ix_labelelements_template_labeltype");
        }
    }

}
