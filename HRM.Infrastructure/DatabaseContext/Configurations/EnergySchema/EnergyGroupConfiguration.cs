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
    public class EnergyGroupConfiguration : IEntityTypeConfiguration<EnergyGroup>
    {
        public void Configure(EntityTypeBuilder<EnergyGroup> entity)
        {
            entity.ToTable("groups", "energy");

            entity.HasKey(x => x.GroupId)
                  .HasName("pk_energy_groups");

            entity.Property(x => x.GroupId)
                  .HasColumnName("group_id")
                  .UseIdentityByDefaultColumn();

            entity.Property(x => x.Code)
                  .HasColumnName("code")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.Name)
                  .HasColumnName("name")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.HasIndex(x => x.Code)
                  .IsUnique()
                  .HasDatabaseName("ux_energy_groups_code");
        }
    }
}
