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
    public class WarehouseShelfLedgerConfiguration : IEntityTypeConfiguration<WarehouseShelfLedger>
    {
        public void Configure(EntityTypeBuilder<WarehouseShelfLedger> entity)
        {
            entity.ToTable("WarehouseShelfLedger", "Warehouse");

            entity.HasKey(x => x.LedgerId)
                  .HasName("PK_WarehouseShelfLedger_LedgerId");

            entity.Property(x => x.LedgerId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("ledgerId");

            entity.Property(x => x.VoucherId).HasColumnName("voucherId");
            entity.Property(x => x.VoucherDetailId).HasColumnName("voucherDetailId");

            entity.Property(x => x.SlotId)
                  .IsRequired()
                  .HasColumnName("slotId");

            entity.Property(x => x.CompanyId)
                  .IsRequired()
                  .HasColumnName("companyId");

            entity.Property(x => x.ProductCode).HasColumnName("productCode");
            entity.Property(x => x.LotNumber).HasColumnName("lotNumber");

            entity.Property(x => x.DeltaKg)
                  .HasPrecision(18, 3)
                  .IsRequired()
                  .HasColumnName("deltaKg");

            entity.Property(x => x.BeforeKg)
                  .HasPrecision(18, 3)
                  .IsRequired()
                  .HasColumnName("beforeKg");

            entity.Property(x => x.AfterKg)
                  .HasPrecision(18, 3)
                  .IsRequired()
                  .HasColumnName("afterKg");

            entity.Property(x => x.StockType)
                    .HasColumnName("stockType")
                    .HasConversion<int>();


            entity.Property(x => x.Reason).HasColumnName("reason");

            entity.Property(x => x.CreatedBy).HasColumnName("createdBy");

            entity.Property(x => x.CreatedAt)
                  .HasColumnName("createdAt")
                  .HasDefaultValueSql("now()");

            entity.Property(x => x.PurposeId).HasColumnName("purposeId");
            entity.Property(x => x.RequestCode).HasColumnName("requestCode");
            entity.Property(x => x.AppSource).HasColumnName("appSource");
            entity.Property(x => x.UnitName).HasDefaultValueSql("'Kg'::text");
            entity.Property(x => x.ExpiryDate).HasColumnName("ExpiryDate");

            // Indexes gợi ý cho tra cứu log
            entity.HasIndex(x => new { x.CompanyId, x.SlotId, x.CreatedAt })
                  .HasDatabaseName("ix_wsledger_company_slot_created");

            entity.HasIndex(x => x.VoucherId)
                  .HasDatabaseName("ix_wsledger_voucher");

            // Ràng buộc: After = Before + Delta
            //entity.HasCheckConstraint("ck_wsledger_flow",
            //    "\"afterKg\" = \"beforeKg\" + \"deltaKg\"");

            // FKs
            entity.HasOne(x => x.Shelf)
                  .WithMany(s => s.Ledgers)
                  .HasForeignKey(x => x.SlotId)
                  .OnDelete(DeleteBehavior.Restrict)      // giữ log khi xoá kệ
                  .HasConstraintName("FK_WarehouseShelfLedger_SlotId");

            entity.HasOne(x => x.Company)
                  .WithMany(c => c.WarehouseShelfLedgers)
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseShelfLedger_Company");

            entity.HasOne(x => x.CreatedByNavigation)
                  .WithMany(e => e.WarehouseShelfLedgerCreatedByNavigations)
                  .HasForeignKey(x => x.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseShelfLedger_CreatedBy");
        }
    }
}
