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
    public class WarehouseShelfStockConfiguration : IEntityTypeConfiguration<WarehouseShelfStock>
    {
        public void Configure(EntityTypeBuilder<WarehouseShelfStock> entity)
        {
            entity.ToTable("WarehouseShelfStock", "Warehouse");

            entity.HasKey(e => e.SlotId)
                  .HasName("PK__WarehouseShelfStock__slotId");

            entity.Property(e => e.ShelfStockId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("ShelfStockId");

            entity.Property(e => e.ShelfStockCode)
                  .HasMaxLength(50)
                  .IsRequired()
                  .HasColumnName("ShelfStockCode");

            entity.Property(e => e.Code)
                  .HasColumnType("citext")
                  .IsRequired()
                  .HasColumnName("code");

            entity.Property(e => e.LotNo)
                  .HasMaxLength(50)
                  .HasColumnName("LotNo");

            entity.Property(e => e.LotKey)
                  .HasColumnType("citext")
                  .HasColumnName("lotKey");

            entity.Property(e => e.StockType)
                  .HasConversion<int>()
                  .HasColumnName("stockType");

            entity.Property(e => e.QtyKg)
                  .HasPrecision(18, 3)
                  .HasColumnName("qtyKg");

            entity.Property(e => e.CompanyId).HasColumnName("companyId");
            entity.Property(e => e.UpdatedBy).HasColumnName("updatedBy");

            entity.HasIndex(x => new { x.CompanyId, x.Code })
                  .HasDatabaseName("IX_WarehouseShelfStock_company_code");

            entity.HasIndex(x => new { x.CompanyId, x.Code, x.LotKey })
                  .HasDatabaseName("IX_WarehouseShelfStock_company_code_lot");

            entity.HasOne(d => d.WarehouseShelves)
                  .WithMany(p => p.WarehouseShelfStocks)
                  .HasForeignKey(d => d.SlotId)
                  .HasConstraintName("FK_WarehouseShelfStock_WarehouseShelf");

            entity.HasOne(d => d.Company)
                  .WithMany(p => p.WarehouseShelfStocks)
                  .HasForeignKey(d => d.CompanyId)
                  .HasConstraintName("FK_WarehouseShelfStock_Company");

            entity.HasOne(d => d.UpdatedByNavigation)
                  .WithMany(p => p.WarehouseShelfStockUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .HasConstraintName("FK_WarehouseShelfStock_UpdatedBy");
        }
    }
}
