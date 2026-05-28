using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.ForscadaSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ForscadaSchema
{
    public sealed class InfoProForPlcConfiguration : IEntityTypeConfiguration<InfoProForPlc>
    {
        public void Configure(EntityTypeBuilder<InfoProForPlc> entity)
        {
            entity.HasNoKey();
            entity.ToTable("infoprofor_plc", "forscada");

            entity.Property(e => e.D0).HasColumnName("d0");
            entity.Property(e => e.D1).HasColumnName("d1");
            entity.Property(e => e.D2).HasColumnName("d2");
            entity.Property(e => e.D3).HasColumnName("d3");
            entity.Property(e => e.D4).HasColumnName("d4");
            entity.Property(e => e.D5).HasColumnName("d5");
            entity.Property(e => e.D6).HasColumnName("d6");
            entity.Property(e => e.D7).HasColumnName("d7");
            entity.Property(e => e.D8).HasColumnName("d8");
            entity.Property(e => e.D9).HasColumnName("d9");
            entity.Property(e => e.MachineId).HasColumnName("machineid").HasColumnType("text");
        }
    }
}
