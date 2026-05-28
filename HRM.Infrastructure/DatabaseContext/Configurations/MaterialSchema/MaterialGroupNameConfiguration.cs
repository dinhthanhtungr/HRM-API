using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.MaterialSchema
{
    public class MaterialGroupNameConfiguration : IEntityTypeConfiguration<MaterialGroupName>
    {
        public void Configure(EntityTypeBuilder<MaterialGroupName> entity)
        {
            entity.HasKey(e => e.MaterialGroupNameId)
                .HasName("PK_MaterialGroupNames");

            entity.ToTable("MaterialGroupNames", "Material");

            entity.HasIndex(e => e.MaterialId, "IX_MaterialGroupNames_MaterialId");

            entity.Property(e => e.MaterialGroupNameId)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.MaterialGroupNameText)
                .IsRequired()
                .HasColumnType("citext");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.HasOne(d => d.Material)
                .WithMany(p => p.MaterialGroupNames)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MaterialGroupNames_Materials");
        }
    }
}
