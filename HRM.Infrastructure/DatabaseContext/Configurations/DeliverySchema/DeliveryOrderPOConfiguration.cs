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
    public sealed class DeliveryOrderPOConfiguration : IEntityTypeConfiguration<DeliveryOrderPO>
    {
        public void Configure(EntityTypeBuilder<DeliveryOrderPO> entity)
        {
            entity.ToTable("DeliveryOrderPO", "DeliveryOrder");

            entity.HasKey(x => new { x.DeliveryOrderId, x.MerchandiseOrderId });

            entity.HasOne(x => x.DeliveryOrder)
                  .WithMany(o => o.DeliveryOrderPOs)
                  .HasForeignKey(x => x.DeliveryOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.MerchandiseOrder)
                  .WithMany(mo => mo.DeliveryOrderPOs)
                  .HasForeignKey(x => x.MerchandiseOrderId)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
