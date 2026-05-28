using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;


public class AreaMROConfiguration : IEntityTypeConfiguration<AreaMRO>
{
    public void Configure(EntityTypeBuilder<AreaMRO> entity)
    {
        entity.ToTable("areas", "mro");

        entity.HasKey(x => x.AreaId).HasName("pk_areas");

        entity.Property(x => x.AreaId)
              .UseIdentityByDefaultColumn()
              .HasColumnName("area_id");

        entity.Property(x => x.AreaExternalId)
              .HasColumnName("area_externalid")
              .HasColumnType("text")
              .IsRequired();

        entity.Property(x => x.AreaName)
              .HasColumnName("area_name")
              .HasColumnType("text")
              .IsRequired();

        entity.HasIndex(x => x.AreaExternalId)
              .HasDatabaseName("ux_areas_area_externalid")
              .IsUnique();
    }
}
