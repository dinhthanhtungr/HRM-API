using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.WarehouseSchema
{
    public class WarehouseRequestDetailConfiguration : IEntityTypeConfiguration<WarehouseRequestDetail>
    {
        public void Configure(EntityTypeBuilder<WarehouseRequestDetail> entity)
        {
            entity.ToTable("WarehouseRequestDetail", "Warehouse");

            entity.HasKey(e => e.DetailId)
                  .HasName("PK__WareHouseRequestDetail__detailId");

            entity.Property(e => e.DetailId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("detailId");

            entity.Property(e => e.RequestId).HasColumnName("requestId");

            entity.Property(e => e.WeightKg)
                  .HasPrecision(18, 3)
                  .HasColumnName("weightKg");
            entity.Property(x => x.IsActive)
                  .HasDefaultValue(true)
                  .HasColumnName("isActive");

            // tên index bạn đang dùng
            entity.HasIndex(e => e.RequestId, "IX_WarehouseRequestDetail_RequestCode");

            entity.HasOne(d => d.WarehouseRequest)
                  .WithMany(p => p.WarehouseRequestDetails)
                  .HasForeignKey(d => d.RequestId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_WarehouseRequestDetail_RequestId");
        }
    }
}
