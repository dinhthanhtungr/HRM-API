using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.MaterialSchema
{
    public class UnitConfiguration : IEntityTypeConfiguration<Unit>
    {
        public void Configure(EntityTypeBuilder<Unit> entity)
        {
            entity.HasKey(e => e.UnitId).HasName("PK__Units__44F5ECB5E080D698");
            entity.ToTable("Units", "Material");

            entity.HasIndex(e => e.CreatedBy, "IX_Units_CreatedBy");

            entity.Property(e => e.UnitId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ExternalId).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Symbol).HasMaxLength(20);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Units)
                  .HasForeignKey(d => d.CreatedBy)
                  .HasConstraintName("FK_Units_CreatedBy");
        }
    }
}
