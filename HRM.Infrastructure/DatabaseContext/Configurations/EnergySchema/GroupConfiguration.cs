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
    internal class GroupConfiguration : IEntityTypeConfiguration<Group>
    {
        public void Configure(EntityTypeBuilder<Group> entity)
        {
            entity.HasKey(e => e.GroupId).HasName("pk_energy_groups");

            entity.ToTable("groups", "energy");

            entity.HasIndex(e => e.Code, "ux_energy_groups_code").IsUnique();

            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.Code)
                .HasColumnType("citext")
                .HasColumnName("code");
            entity.Property(e => e.Name)
                .HasColumnType("citext")
                .HasColumnName("name");
        }
    }
}
