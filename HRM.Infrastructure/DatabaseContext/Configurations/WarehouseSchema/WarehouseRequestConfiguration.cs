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
    public class WarehouseRequestConfiguration : IEntityTypeConfiguration<WarehouseRequest>
    {
        public void Configure(EntityTypeBuilder<WarehouseRequest> entity)
        {
            entity.ToTable("WarehouseRequest", "Warehouse");

            entity.HasKey(e => e.RequestId)
                  .HasName("PK__WareHouseRequest__3214EC07A98DEC4E");

            entity.Property(e => e.RequestId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("requestId");

            entity.Property(e => e.RequestName)
                  .IsRequired()
                  .HasColumnName("requestName");

            entity.Property(e => e.ReqStatus)
                  .HasColumnName("reqStatus"); // enum -> int mặc định

            entity.Property(e => e.ReqType)
                  .HasColumnName("reqType");   // enum -> int mặc định

            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.UpdatedBy).HasColumnName("updatedBy");

            entity.HasOne(d => d.Company)
                  .WithMany(p => p.WarehouseRequests)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_WarehouseRequest_Company");

            entity.HasOne(d => d.CreatedByNavigation)
                  .WithMany(p => p.WarehouseRequestCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_WarehouseRequest_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation)
                  .WithMany(p => p.WarehouseRequestUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_WarehouseRequest_UpdatedBy");
        }
    }
}
