using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryTripConfiguration : IEntityTypeConfiguration<DeliveryTrip>
    {
        public void Configure(EntityTypeBuilder<DeliveryTrip> entity)
        {
            entity.ToTable("DeliveryTrips", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK_DeliveryTrips");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.Property(e => e.ExternalId).IsRequired().HasColumnType("citext");
            entity.Property(e => e.Status).IsRequired().HasColumnType("citext");
            entity.Property(e => e.RouteCode).HasColumnType("citext");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.CompanyId, "IX_DeliveryTrips_CompanyId");
            entity.HasIndex(e => e.VehicleId, "IX_DeliveryTrips_VehicleId");
            entity.HasIndex(e => e.DriverId, "IX_DeliveryTrips_DriverId");
            entity.HasIndex(e => e.DispatcherId, "IX_DeliveryTrips_DispatcherId");
            entity.HasIndex(e => new { e.CompanyId, e.ExternalId }, "UX_DeliveryTrips_Company_ExternalId")
                  .IsUnique();

            entity.HasOne(e => e.Vehicle)
                  .WithMany(v => v.Trips)
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryTrips_Vehicle");

            entity.HasOne(e => e.Driver)
                  .WithMany(d => d.DeliveryTrips)
                  .HasForeignKey(e => e.DriverId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryTrips_Driver");

            entity.HasOne<Company>()
                  .WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_DeliveryTrips_Company");

            entity.HasOne<Employee>()
                  .WithMany()
                  .HasForeignKey(e => e.DispatcherId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryTrips_Dispatcher");
        }
    }
}
