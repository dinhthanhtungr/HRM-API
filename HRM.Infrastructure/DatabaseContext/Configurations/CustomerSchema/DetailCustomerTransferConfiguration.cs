using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema
{
    public class DetailCustomerTransferConfiguration : IEntityTypeConfiguration<DetailCustomerTransfer>
    {
        public void Configure(EntityTypeBuilder<DetailCustomerTransfer> entity)
        {
            entity.HasKey(e => new { e.LogId, e.CustomerId }).HasName("PK_DetailCustomerTransfer");
            entity.ToTable("DetailCustomerTransfer", "Customer");

            entity.Property(e => e.LogId).HasColumnName("LogId");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerId");

            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_DetailCustomerTransfer_CustomerId");

            // XÓA LOG -> XÓA DETAIL
            entity.HasOne(d => d.Log).WithMany(p => p.DetailCustomerTransfers)
                  .HasForeignKey(d => d.LogId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DetailCustomerTransfer_Log");

            // XÓA CUSTOMER -> XÓA DETAIL (đổi từ Restrict sang Cascade theo yêu cầu)
            entity.HasOne(d => d.Customer).WithMany(p => p.DetailCustomerTransfers)
                  .HasForeignKey(d => d.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DetailCustomerTransfer_Customer");
        }
    }
}
