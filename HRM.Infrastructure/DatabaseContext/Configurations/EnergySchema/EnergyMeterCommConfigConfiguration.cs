using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.EnergyScheme;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.EnergySchema
{
    public class EnergyMeterCommConfigConfiguration : IEntityTypeConfiguration<EnergyMeterCommConfig>
    {
        public void Configure(EntityTypeBuilder<EnergyMeterCommConfig> entity)
        {
            entity.ToTable("meter_comm_config", "energy");

            entity.HasKey(x => x.MeterId)
                  .HasName("pk_energy_meter_comm_config");

            entity.Property(x => x.MeterId).HasColumnName("meter_id");
            entity.Property(x => x.Protocol).HasColumnName("protocol").HasColumnType("text");
            entity.Property(x => x.SerialPort).HasColumnName("serial_port").HasColumnType("text");
            entity.Property(x => x.BaudRate).HasColumnName("baud_rate");
            entity.Property(x => x.Parity).HasColumnName("parity").HasColumnType("text");
            entity.Property(x => x.DataBits).HasColumnName("data_bits");
            entity.Property(x => x.StopBits).HasColumnName("stop_bits");
            entity.Property(x => x.SlaveId).HasColumnName("slave_id");
            entity.Property(x => x.RegKwhAddr).HasColumnName("reg_kwh_addr");
            entity.Property(x => x.RegKwhLen).HasColumnName("reg_kwh_len");
            entity.Property(x => x.WordOrder).HasColumnName("word_order").HasColumnType("text");

            entity.Property(x => x.Scale)
                  .HasColumnName("scale")
                  .HasPrecision(12, 6);

            entity.HasOne(x => x.Meter)
                  .WithOne()
                  .HasForeignKey<EnergyMeterCommConfig>(x => x.MeterId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_energy_mcc_meter");
        }
    }
}
