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
    public class ShiftLeaderForRecordToPlcConfiguration : IEntityTypeConfiguration<ShiftLeaderForRecordToPlc>
    {
        public void Configure(EntityTypeBuilder<ShiftLeaderForRecordToPlc> entity)
        {
            entity.ToTable("shiftleaderforrecordto_plc", "forscada");
            entity.HasNoKey();

            entity.Property(x => x.D0).HasColumnName("d0");
            entity.Property(x => x.D1).HasColumnName("d1");
            entity.Property(x => x.D2).HasColumnName("d2");
            entity.Property(x => x.D3).HasColumnName("d3");
            entity.Property(x => x.D4).HasColumnName("d4");
            entity.Property(x => x.D5).HasColumnName("d5");
            entity.Property(x => x.D6).HasColumnName("d6");
            entity.Property(x => x.D7).HasColumnName("d7");
            entity.Property(x => x.D8).HasColumnName("d8");
            entity.Property(x => x.D9).HasColumnName("d9");
            entity.Property(x => x.D10).HasColumnName("d10");
            entity.Property(x => x.D11).HasColumnName("d11");
            entity.Property(x => x.MachineId).HasColumnName("machineid").HasColumnType("text");
        }
    }
}
