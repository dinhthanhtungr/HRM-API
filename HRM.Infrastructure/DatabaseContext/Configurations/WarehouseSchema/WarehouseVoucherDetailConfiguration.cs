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
    public class WarehouseVoucherDetailConfiguration : IEntityTypeConfiguration<WarehouseVoucherDetail>
    {
        public void Configure(EntityTypeBuilder<WarehouseVoucherDetail> entity)
        {
            entity.ToTable("WarehouseVoucherDetails", "Warehouse");

            entity.HasKey(x => x.VoucherDetailId)
                  .HasName("PK__WarehouseVoucherDetails__voucherDetailId");

            entity.Property(x => x.VoucherDetailId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("voucherDetailId");

            entity.Property(x => x.VoucherId)
                  .HasColumnName("voucherId");

            entity.Property(x => x.LineNo)
                  .HasColumnName("lineNo");

            entity.Property(x => x.ProductCode)
                  .HasColumnName("productCode")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.ProductName)
                  .HasColumnName("productName")
                  .IsRequired();

            entity.Property(x => x.LotNumber)
                  .HasColumnName("lotNumber")
                  .HasColumnType("citext");

            entity.Property(x => x.QtyKg)
                  .HasColumnName("qtyKg")
                  .HasPrecision(10, 2);

            entity.Property(x => x.Bags)
                  .HasColumnName("bags");

            entity.Property(x => x.SlotId)
                  .HasColumnName("slotId");

            entity.Property(x => x.PurposeId)
                  .HasColumnName("purposeId");

            entity.Property(x => x.IsIncrease)
                  .HasColumnName("isIncrease");

            entity.Property(x => x.Note)
                  .HasColumnName("note");

            entity.Property(x => x.IsApplied)
                  .HasColumnName("isApplied")
                  .HasDefaultValue(false);

            entity.Property(x => x.UnitName)
                  .HasDefaultValueSql("'Kg'::text");

            entity.Property(x => x.ExpiryDate)
                  .HasColumnName("expirydate")
                  .HasDefaultValueSql("'-infinity'::timestamp without time zone")
                  .IsRequired(false);

            entity.Property(x => x.VoucherType)
                  .HasColumnName("voucherType")
                  .HasConversion<int>();

            // Dòng phải duy nhất trong 1 phiếu
            entity.HasIndex(x => new { x.VoucherId, x.LineNo })
                  .IsUnique()
                  .HasDatabaseName("ux_voucherdetails_voucher_lineno");

            entity.HasIndex(x => x.SlotId)
                  .HasDatabaseName("ix_voucherdetails_slot");

            // FK
            entity.HasOne(x => x.Voucher)
                  .WithMany(v => v.Details)
                  .HasForeignKey(x => x.VoucherId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_WarehouseVoucherDetails_Voucher");

            entity.HasOne(x => x.Slot)
                  .WithMany(x => x.WarehouseVoucherDetails)
                  .HasForeignKey(x => x.SlotId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseVoucherDetails_Slot");

            entity.HasOne(x => x.Purpose)
                  .WithMany(p => p.VoucherDetails)
                  .HasForeignKey(x => x.PurposeId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseVoucherDetails_Purpose");
        }
    }
}
