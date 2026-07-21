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
    public class TariffConfiguration : IEntityTypeConfiguration<Tariff>
    {
        public void Configure(EntityTypeBuilder<Tariff> entity)
        {
            entity.HasKey(e => e.TariffId).HasName("pk_energy_tariffs");

            entity.ToTable("tariffs", "energy");

            entity.HasIndex(e => e.Code, "ux_energy_tariffs_code").IsUnique();

            entity.Property(e => e.TariffId).HasColumnName("tariff_id");
            entity.Property(e => e.Code)
                .HasColumnType("citext")
                .HasColumnName("code");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::citext")
                .HasColumnType("citext")
                .HasColumnName("currency");
            entity.Property(e => e.Name)
                .HasColumnType("citext")
                .HasColumnName("name");
            entity.Property(e => e.Note)
                .HasColumnType("citext")
                .HasColumnName("note");
            entity.Property(e => e.Utility)
                .HasColumnType("citext")
                .HasColumnName("utility");
        }
    }
}
