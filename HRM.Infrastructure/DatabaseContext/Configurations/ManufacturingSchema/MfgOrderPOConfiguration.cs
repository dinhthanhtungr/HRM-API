using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.ManufacturingSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.ManufacturingSchema
{
    public class MfgOrderPOConfiguration : IEntityTypeConfiguration<MfgOrderPO>
    {
        public void Configure(EntityTypeBuilder<MfgOrderPO> entity)
        {
            entity.ToTable("MfgOrderPOs", "manufacturing");

            entity.HasKey(x => new { x.MerchandiseOrderDetailId, x.MfgProductionOrderId })
                  .HasName("PK_MfgOrderPOs");

            entity.Property(x => x.MerchandiseOrderDetailId).HasColumnName("MerchandiseOrderDetailId");
            entity.Property(x => x.MfgProductionOrderId).HasColumnName("MfgProductionOrderId");
            entity.Property(x => x.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

            entity.HasIndex(x => x.MerchandiseOrderDetailId).HasDatabaseName("IX_MfgOrderPOs_DetailId");
            entity.HasIndex(x => x.MfgProductionOrderId).HasDatabaseName("IX_MfgOrderPOs_MfgOrderId");

            entity.HasIndex(x => new { x.MerchandiseOrderDetailId, x.IsActive })
                  .HasDatabaseName("UX_MfgOrderPOs_Detail_Active");

            entity.HasIndex(x => new { x.MfgProductionOrderId, x.IsActive })
                  .HasDatabaseName("UX_MfgOrderPOs_MfgOrder_Active");

            entity.HasOne(x => x.Detail).WithMany()
                  .HasForeignKey(x => x.MerchandiseOrderDetailId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_MfgOrderPOs_Detail");

            entity.HasOne(x => x.ProductionOrder).WithMany()
                  .HasForeignKey(x => x.MfgProductionOrderId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_MfgOrderPOs_MfgOrder");
        }
    }
}
