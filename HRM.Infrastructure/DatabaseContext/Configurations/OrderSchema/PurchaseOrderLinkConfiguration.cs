using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema
{
    public class PurchaseOrderLinkConfiguration : IEntityTypeConfiguration<PurchaseOrderLink>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrderLink> entity)
        {
            entity.HasKey(e => e.PurchaseOrderLinkId)
                  .HasName("PK__PurchaseOrderLink__5026B698A94EFE60");

            entity.ToTable("PurchaseOrderLink", "Orders");

            entity.Property(e => e.PurchaseOrderLinkId)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("PurchaseOrderLinkId");

            entity.HasOne(x => x.PurchaseOrder)
                  .WithMany(o => o.PurchaseOrderLinks)
                  .HasForeignKey(x => x.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.MerchandiseOrder)
                  .WithMany(mo => mo.PurchaseOrderLinks)
                  .HasForeignKey(x => x.MerchandiseOrderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SupplyRequest)
                  .WithMany(mo => mo.PurchaseOrderLinks)
                  .HasForeignKey(x => x.SupplyRequestId)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
