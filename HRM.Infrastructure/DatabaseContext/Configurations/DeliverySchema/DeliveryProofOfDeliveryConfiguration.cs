using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryProofOfDeliveryConfiguration : IEntityTypeConfiguration<DeliveryProofOfDelivery>
    {
        public void Configure(EntityTypeBuilder<DeliveryProofOfDelivery> entity)
        {
            entity.ToTable("DeliveryProofsOfDelivery", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK_DeliveryProofsOfDelivery");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.Property(e => e.ReceiverName).HasColumnType("citext");
            entity.Property(e => e.ReceiverPhone).HasColumnType("citext");
            entity.Property(e => e.SignatureUrl).HasMaxLength(1000);
            entity.Property(e => e.PhotoUrl).HasMaxLength(1000);
            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => e.DeliveryOrderId, "IX_DeliveryProofsOfDelivery_DeliveryOrderId");
            entity.HasIndex(e => e.ConfirmedBy, "IX_DeliveryProofsOfDelivery_ConfirmedBy");

            entity.HasOne(e => e.DeliveryOrder)
                  .WithMany(o => o.ProofsOfDelivery)
                  .HasForeignKey(e => e.DeliveryOrderId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DeliveryProofsOfDelivery_Order");

            entity.HasOne<Employee>()
                  .WithMany()
                  .HasForeignKey(e => e.ConfirmedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryProofsOfDelivery_ConfirmedBy");
        }
    }
}
