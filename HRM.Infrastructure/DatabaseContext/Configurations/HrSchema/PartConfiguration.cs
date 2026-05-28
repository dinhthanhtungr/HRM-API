using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class PartConfiguration : IEntityTypeConfiguration<Part>
    {
        public void Configure(EntityTypeBuilder<Part> entity)
        {
            entity.HasKey(e => e.PartId).HasName("PK__Parts__7C3F0D30F786F0A7");
            entity.ToTable("Parts", "hr");

            entity.Property(e => e.PartId)
                  .HasColumnName("PartID")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.PartName).HasColumnType("citext");
            entity.Property(e => e.Description).HasColumnType("citext");
            entity.Property(e => e.ExternalId).HasColumnName("ExternalID").HasColumnType("citext");
        }
    }
}
