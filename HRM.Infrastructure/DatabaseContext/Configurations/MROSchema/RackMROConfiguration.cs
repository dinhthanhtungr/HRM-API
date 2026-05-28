using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;

public class RackMROConfiguration : IEntityTypeConfiguration<RackMRO>
{
    public void Configure(EntityTypeBuilder<RackMRO> entity)
    {
        entity.HasKey(e => e.RackId).HasName("PK__Ranks__3214EC07A98DEC4E");
        entity.Property(x => x.RackId).UseIdentityAlwaysColumn();

        entity.ToTable("racks", "mro");
        entity.Property(x => x.RackId).HasColumnName("rack_id");
        entity.Property(x => x.ZoneId).HasColumnName("zone_id");
        entity.Property(x => x.RackExternalId)
              .HasColumnName("rack_external_id")
              .HasColumnType("text")
              .IsRequired();
        entity.Property(x => x.RackName)
              .HasColumnName("rack_name")
              .HasColumnType("text")
              .IsRequired();

        entity.HasIndex(x => x.ZoneId).HasDatabaseName("ix_racks_zone_id");

        entity.HasOne(r => r.Zone)
              .WithMany(z => z.Racks)
              .HasForeignKey(r => r.ZoneId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_racks_zone_id");

        entity.HasIndex(x => new { x.ZoneId, x.RackExternalId })
              .HasDatabaseName("ux_racks_zone_external")
              .IsUnique();
    }
}
