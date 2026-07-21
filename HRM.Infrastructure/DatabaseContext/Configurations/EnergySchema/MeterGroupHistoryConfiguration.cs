using HRM.Domain.Entities.EnergyScheme;
using HRM.Domain.Entities.HistoryRecordSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Infrastructure.DatabaseContext.Configurations.EnergySchema
{
    public class MeterGroupHistoryConfiguration : IEntityTypeConfiguration<MeterGroupHistory>
    {
        public void Configure(EntityTypeBuilder<MeterGroupHistory> entity)
        {
            entity.HasKey(e => new { e.MeterId, e.ValidFrom }).HasName("pk_energy_meter_group_history");

            entity.ToTable("meter_group_history", "energy");

            entity.HasIndex(e => e.GroupId, "IX_meter_group_history_group_id");

            entity.HasIndex(e => e.MeterId, "ix_energy_mgh_meter");

            entity.HasIndex(e => new { e.MeterId, e.ValidFrom }, "ix_energy_mgh_meter_validfrom_desc").IsDescending(false, true);

            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.ValidFrom)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("valid_from");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.ValidTo)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("valid_to");

            entity.HasOne(d => d.Group).WithMany(p => p.MeterGroupHistories)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_energy_mgh_group");

            entity.HasOne(d => d.Meter).WithMany(p => p.MeterGroupHistories)
                .HasForeignKey(d => d.MeterId)
                .HasConstraintName("fk_energy_mgh_meter");
        }
    }
}
