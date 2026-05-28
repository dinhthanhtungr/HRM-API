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
    public class WarehouseVoucherConfiguration : IEntityTypeConfiguration<WarehouseVoucher>
    {
        public void Configure(EntityTypeBuilder<WarehouseVoucher> entity)
        {
            entity.ToTable("WarehouseVouchers", "Warehouse");

            entity.HasKey(x => x.VoucherId)
                  .HasName("PK__WarehouseVouchers__voucherId");

            entity.Property(x => x.VoucherId)
                  .UseIdentityAlwaysColumn()
                  .HasColumnName("voucherId");

            entity.Property(x => x.VoucherCode)
                  .HasColumnName("voucherCode")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.VoucherType)
                  .HasColumnName("voucherType");

            entity.Property(x => x.RequestId)
                  .HasColumnName("requestId");

            entity.Property(x => x.CompanyId)
                  .HasColumnName("companyId");

            entity.Property(x => x.CreatedBy)
                  .HasColumnName("createdBy");

            entity.Property(x => x.CreatedDate)
                  .HasColumnName("createdDate");

            entity.Property(x => x.Status)
                  .HasColumnName("status");

            // (CompanyId, VoucherCode) duy nhất
            entity.HasIndex(x => new { x.CompanyId, x.VoucherCode })
                  .IsUnique()
                  .HasDatabaseName("ux_vouchers_company_code");

            entity.HasIndex(x => new { x.CompanyId, x.CreatedDate })
                  .HasDatabaseName("ix_vouchers_company_created");

            // FK
            entity.HasOne(x => x.Company)
                  .WithMany(x => x.WarehouseVouchers) // tránh phải thêm collection vào Company
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseVouchers_Company");

            entity.HasOne(x => x.CreatedByNavigation)
                  .WithMany(x => x.WarehouseVoucherCreatedByNavigations)
                  .HasForeignKey(x => x.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseVouchers_CreatedBy");

            entity.HasOne(x => x.WarehouseRequest)
                  .WithMany(x => x.WarehouseVouchers)
                  .HasForeignKey(x => x.RequestId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_WarehouseVouchers_Request");
        }
    }
}
