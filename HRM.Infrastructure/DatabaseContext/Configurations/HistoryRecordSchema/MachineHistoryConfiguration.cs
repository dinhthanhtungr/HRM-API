using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HistoryRecordSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HistoryRecordSchema
{
    public class MachineHistoryConfiguration : IEntityTypeConfiguration<MachineHistory>
    {
        public void Configure(EntityTypeBuilder<MachineHistory> entity)
        {
            entity.ToTable("machine_history", schema: "historyrecords"); // tên bảng PascalCase như hình
            entity.HasNoKey();
            entity.Property(x => x.Id)
                  .HasColumnName("machine_history_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(x => x.CreatedAt)
                  .HasColumnName("created_at");

            entity.Property(x => x.ProducingTimeOfDay).HasColumnName("producing_time_of_day");
            entity.Property(x => x.WaitingTimeOfDay).HasColumnName("waiting_time_of_day");
            entity.Property(x => x.MachineCleansingTimeOfDay).HasColumnName("machine_cleansing_time_of_day");

            entity.Property(x => x.EnergyTotalOfDay).HasColumnName("energy_total_of_day");
            entity.Property(x => x.ProducingEnergyOfDay).HasColumnName("producing_energy_of_day");
            entity.Property(x => x.MachineCleansingEnergyOfDay).HasColumnName("machine_cleansing_energy_of_day");
            entity.Property(x => x.WaitingEnergyOfDay).HasColumnName("waiting_energy_of_day");

            // Index gợi ý
            entity.HasIndex(x => x.CreatedAt)
                  .HasDatabaseName("ix_machinehistory_created_at");
        }
    }
}
