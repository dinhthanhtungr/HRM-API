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
    public class LabelTemplateConfiguration : IEntityTypeConfiguration<LabelTemplate>
    {
        public void Configure(EntityTypeBuilder<LabelTemplate> entity)
        {
            entity.ToTable("labeltemplates", "printect");
            entity.HasNoKey();
            entity.Property(x => x.TemplateId).HasColumnName("templateid").UseIdentityAlwaysColumn();

            entity.Property(x => x.WidthMm).HasColumnName("widthmm");
            entity.Property(x => x.HeightMm).HasColumnName("heightmm");
            entity.Property(x => x.LabelType).HasColumnName("labeltype").HasColumnType("citext");

            entity.HasIndex(x => new { x.WidthMm, x.HeightMm, x.LabelType })
                  .IsUnique().HasDatabaseName("ux_labeltemplates_size_type");
        }
    }

}
