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
    public class WarehouseShelfConfiguration : IEntityTypeConfiguration<WarehouseShelves>
    {
        public void Configure(EntityTypeBuilder<WarehouseShelves> entity)
        {
            entity.ToTable("WarehouseShelves", "Warehouse");

            entity.HasKey(x => x.SlotId)
                  .HasName("PK_WarehouseShelves_SlotId");

            entity.Property(x => x.SlotId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("ShelfStockId");

            entity.Property(x => x.SlotCode)
                  .HasMaxLength(64)
                  .IsRequired()
                  .HasColumnName("ShelfStockCode");

            entity.Property(x => x.CompanyId)
                  .IsRequired()
                  .HasColumnName("companyId");

            entity.Property(x => x.CurrentWeightKg)
                  .HasPrecision(10, 2)
                  .IsRequired()
                  .HasColumnName("currentWeightKg");

            entity.Property(x => x.MaxWeightKg)
                  .HasPrecision(10, 2)
                  .IsRequired()
                  .HasColumnName("maxWeightKg");

            entity.Property(x => x.IsActive)
                  .HasDefaultValue(true)
                  .IsRequired()
                  .HasColumnName("isActive");

            entity.Property(x => x.LastUpdated)
                  .HasColumnName("lastUpdated")
                  .HasDefaultValueSql("now()");

            // Indexes
            entity.HasIndex(x => new { x.CompanyId, x.SlotCode })
                  .IsUnique()
                  .HasDatabaseName("ux_warehouseshelves_company_slotcode");

            entity.HasIndex(x => x.LastUpdated)
                  .HasDatabaseName("ix_warehouseshelves_lastupdated");

            // FK
            entity.HasOne(x => x.Company)
                  .WithMany(c => c.WarehouseShelves)
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseShelves_Company");
        }
    }
}
