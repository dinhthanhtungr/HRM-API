using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryVehicleConfiguration : IEntityTypeConfiguration<DeliveryVehicle>
    {
        public void Configure(EntityTypeBuilder<DeliveryVehicle> entity)
        {
            entity.ToTable("DeliveryVehicles", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK_DeliveryVehicles");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.Property(e => e.PlateNumber).IsRequired().HasColumnType("citext");
            entity.Property(e => e.VehicleType).HasColumnType("citext");
            entity.Property(e => e.MaxLoadKg).HasPrecision(18, 3);
            entity.Property(e => e.MaxVolumeM3).HasPrecision(18, 3);
            entity.Property(e => e.OwnerName).HasColumnType("citext");
            entity.Property(e => e.Phone).HasColumnType("citext");
            entity.Property(e => e.IsInternal).HasDefaultValue(true);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => e.CompanyId, "IX_DeliveryVehicles_CompanyId");
            entity.HasIndex(e => new { e.CompanyId, e.PlateNumber }, "UX_DeliveryVehicles_Company_PlateNumber")
                  .IsUnique();

            entity.HasOne<Company>()
                  .WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_DeliveryVehicles_Company");
        }
    }
}
