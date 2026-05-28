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
    public class EnergyTariffConfiguration : IEntityTypeConfiguration<EnergyTariff>
    {
        public void Configure(EntityTypeBuilder<EnergyTariff> entity)
        {
            entity.ToTable("tariffs", "energy");

            entity.HasKey(x => x.TariffId)
                  .HasName("pk_energy_tariffs");

            entity.Property(x => x.TariffId)
                  .HasColumnName("tariff_id")
                  .UseIdentityByDefaultColumn();

            entity.Property(x => x.Code)
                  .HasColumnName("code")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.Name)
                  .HasColumnName("name")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.Currency)
                  .HasColumnName("currency")
                  .HasColumnType("citext")
                  .HasDefaultValue("VND");

            entity.Property(x => x.Utility)
                  .HasColumnName("utility")
                  .HasColumnType("citext");

            entity.Property(x => x.Note)
                  .HasColumnName("note")
                  .HasColumnType("citext");

            entity.HasIndex(x => x.Code)
                  .IsUnique()
                  .HasDatabaseName("ux_energy_tariffs_code");
        }
    }
}
