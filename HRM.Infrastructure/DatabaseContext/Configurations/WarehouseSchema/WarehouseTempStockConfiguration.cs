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
    public class WarehouseTempStockConfiguration : IEntityTypeConfiguration<WarehouseTempStock>
    {
        public void Configure(EntityTypeBuilder<WarehouseTempStock> entity)
        {
            entity.ToTable("WarehouseTempStock", "Warehouse");

            entity.HasKey(e => e.TempId)
                  .HasName("PK__WarehouseTempStock__tempId");

            entity.Property(e => e.TempId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("tempId");

            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");

            entity.Property(e => e.Code)
                  .HasColumnType("citext")
                  .IsRequired()
                  .HasColumnName("code");

            entity.Property(e => e.VaCode)
                  .HasColumnType("citext")
                  .IsRequired()
                  .HasColumnName("vaCode");

            entity.Property(e => e.LotKey)
                  .HasColumnType("citext")
                  .HasColumnName("lotKey");

            entity.Property(e => e.QtyRequest)
                  .HasPrecision(18, 3)
                  .HasColumnName("qtyRequest");

            entity.Property(e => e.ReserveStatus)
                  .HasColumnName("reserveStatus"); // enum -> int mặc định

            entity.HasIndex(x => new { x.CompanyId, x.VaCode })
                  .HasDatabaseName("IX_WarehouseTempStock_company_va");

            entity.HasOne(d => d.CreatedByNavigation)
                  .WithMany(p => p.WarehouseTempStockCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseTempStock_CreatedBy");
        }
    }
}
