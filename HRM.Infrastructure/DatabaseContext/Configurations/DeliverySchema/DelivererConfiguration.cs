using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DelivererConfiguration : IEntityTypeConfiguration<Deliverer>
    {
        public void Configure(EntityTypeBuilder<Deliverer> entity)
        {
            entity.ToTable("Deliverer", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK__Deliverer__3214EC27D1AFC3E3");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.HasIndex(e => e.DelivererInforId, "IX_Deliverer_DelivererInforId");
            entity.HasIndex(e => e.DeliveryOrderId, "IX_Deliverer_DeliveryOrderId");

            // FK -> DelivererInfor
            entity.HasOne(d => d.DelivererInfor)
                  .WithMany()
                  .HasForeignKey(d => d.DelivererInforId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Deliverer_DelivererInforId");

            // FK -> DeliveryOrder
            entity.HasOne(d => d.DeliveryOrder)
                  .WithMany(p => p.Deliverers)
                  .HasForeignKey(d => d.DeliveryOrderId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Deliverer_DeliveryOrderId");
        }
    }
}
