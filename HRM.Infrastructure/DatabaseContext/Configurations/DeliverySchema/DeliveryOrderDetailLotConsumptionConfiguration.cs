using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryOrderDetailLotConsumptionConfiguration : IEntityTypeConfiguration<DeliveryOrderDetailLotConsumption>
    {
        public void Configure(EntityTypeBuilder<DeliveryOrderDetailLotConsumption> entity)
        {
            entity.ToTable("DeliveryOrderDetailLotConsumptions", "DeliveryOrder");

            entity.HasKey(x => x.Id)
                  .HasName("PK_DeliveryOrderDetailLotConsumptions");

            entity.Property(x => x.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("Id");

            entity.Property(x => x.DeliveryOrderDetailId)
                  .HasColumnName("DeliveryOrderDetailId");

            entity.Property(x => x.LotNo)
                  .HasColumnName("LotNo")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.Quantity)
                  .HasColumnName("Quantity")
                  .HasPrecision(18, 3);

            entity.Property(x => x.UnitCostSnapshot)
                  .HasColumnName("UnitCostSnapshot")
                  .HasPrecision(18, 6);

            entity.Property(x => x.TotalCostSnapshot)
                  .HasColumnName("TotalCostSnapshot")
                  .HasPrecision(18, 2);

            entity.Property(x => x.CreatedBy)
                  .HasColumnName("CreatedBy");

            entity.Property(x => x.CreatedDate)
                  .HasColumnName("CreatedDate");

            entity.Property(x => x.IsActive)
                  .HasColumnName("IsActive")
                  .HasDefaultValue(true);

            entity.HasIndex(x => new { x.DeliveryOrderDetailId, x.LotNo })
                  .IsUnique()
                  .HasDatabaseName("UX_DeliveryOrderDetailLotConsumptions_Detail_LotNo");

            entity.HasIndex(x => x.CreatedBy)
                  .HasDatabaseName("IX_DeliveryOrderDetailLotConsumptions_CreatedBy");

            entity.HasOne(x => x.DeliveryOrderDetail)
                  .WithMany(x => x.LotConsumptions)
                  .HasForeignKey(x => x.DeliveryOrderDetailId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DeliveryOrderDetailLotConsumptions_DeliveryOrderDetail");

            entity.HasOne(x => x.CreatedByNavigation)
                  .WithMany()
                  .HasForeignKey(x => x.CreatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryOrderDetailLotConsumptions_CreatedBy");
        }
    }
}
