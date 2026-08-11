using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema
{

    public class MerchandiseOrderConfiguration : IEntityTypeConfiguration<MerchandiseOrder>
    {
        public void Configure(EntityTypeBuilder<MerchandiseOrder> entity)
        {
            // PK
            entity.HasKey(e => e.MerchandiseOrderId)
                  .HasName("PK__Merchand__D0AB7E7AFDA62167");

            // Bảng + schema
            entity.ToTable("MerchandiseOrders", "Orders", table =>
                table.HasCheckConstraint(
                    "CK_MerchandiseOrders_ComplaintReport_OrderType",
                    "\"ComplaintReportId\" IS NULL OR \"OrderType\" = 2"));

            // ===== Columns =====
            entity.Property(e => e.MerchandiseOrderId)
                 .HasColumnName("MerchandiseOrderId")
                 .ValueGeneratedOnAdd()
                 .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.OrderType)
                  .HasColumnName("OrderType")
                  .HasConversion<int>()
                  .HasDefaultValue(OrderType.Merchandise)
                  .IsRequired();

            entity.Property(e => e.ExternalId)
                 .HasColumnName("ExternalId")
                 .HasColumnType("citext");

            entity.Property(e => e.ComplaintReportId)
                 .HasColumnName("ComplaintReportId");

            entity.Property(e => e.DeliveryAddress)
                 .HasColumnName("DeliveryAddress")
                 .HasMaxLength(255);

            entity.Property(e => e.PaymentType)
                 .HasColumnName("PaymentType")
                 .HasColumnType("citext");

            entity.Property(e => e.Receiver)
                 .HasColumnName("Receiver")
                 .HasColumnType("citext");

            entity.Property(e => e.ShippingMethod)
                 .HasColumnName("ShippingMethod")
                 .HasColumnType("citext");

            entity.Property(e => e.Status)
                 .HasColumnName("Status")
                 .HasColumnType("citext");

            entity.Property(e => e.CustomerExternalIdSnapshot)
                 .HasColumnName("CustomerExternalIdSnapshot")
                 .HasColumnType("citext");

            entity.Property(e => e.CustomerNameSnapshot)
                 .HasColumnName("CustomerNameSnapshot")
                 .HasColumnType("citext");

            entity.Property(e => e.TotalPrice)
                 .HasColumnName("TotalPrice")
                 .HasPrecision(18, 2);

            entity.Property(e => e.Vat)
                 .HasColumnName("VAT")
                 .HasPrecision(5, 2);

            entity.Property(e => e.Currency)
                 .HasColumnName("Currency")
                 .HasMaxLength(10)
                 .IsUnicode(false);

            entity.Property(e => e.ExchangeRate)
                  .HasColumnName("ExchangeRate")
                  .HasPrecision(18, 4);


            entity.Property(e => e.IsPaid)
                 .HasColumnName("IsPaid")
                 .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                 .HasColumnName("IsActive")
                 .HasDefaultValue(true);

            entity.Property(e => e.PONo)
                 .HasColumnName("PONo")
                 .HasColumnType("citext");

            entity.Property(e => e.CreateDate)
                 .HasColumnName("CreateDate");

            entity.Property(e => e.UpdatedDate)
                 .HasColumnName("UpdatedDate");

            entity.Property(e => e.IsDeliveryPaused)
                 .HasColumnName("IsDeliveryPaused")
                 .HasDefaultValue(false);

            entity.Property(e => e.DeliveryPausedFrom)
                 .HasColumnName("DeliveryPausedFrom");

            entity.Property(e => e.DeliveryPausedTo)
                 .HasColumnName("DeliveryPausedTo");

            entity.Property(e => e.DeliveryPauseReason)
                 .HasColumnName("DeliveryPauseReason")
                 .HasColumnType("text");

            entity.Property(e => e.DeliveryPauseType)
                 .HasColumnName("DeliveryPauseType")
                 .HasColumnType("citext");

            entity.Property(e => e.DeliveryPausedBy)
                 .HasColumnName("DeliveryPausedBy");

            // ===== Indexes =====
            entity.HasIndex(e => new { e.CompanyId, e.ExternalId })
                 .IsUnique()
                 .HasDatabaseName("UX_MO_Company_ExternalId");

            entity.HasIndex(e => e.CompanyId).HasDatabaseName("IX_MerchandiseOrders_CompanyId");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_MerchandiseOrders_CustomerId");
            entity.HasIndex(e => e.ManagerById).HasDatabaseName("IX_MerchandiseOrders_ManagerById");
            entity.HasIndex(e => e.AttachmentCollectionId).HasDatabaseName("IX_Order_AttachmentCollection");
            entity.HasIndex(e => e.ComplaintReportId)
                 .IsUnique()
                 .HasFilter("\"ComplaintReportId\" IS NOT NULL AND \"IsActive\" = TRUE")
                 .HasDatabaseName("UX_MerchandiseOrders_Active_ComplaintReport");

            // EF Core 8: sort index (CreateDate DESC, PK DESC) + filter Active
            entity.HasIndex(e => new { e.CompanyId, e.CreateDate, e.MerchandiseOrderId })
                 .IsDescending(false, true, true)
                 .HasFilter("\"IsActive\" = TRUE")
                 .HasDatabaseName("IX_MO_Tenant_Active_CreateDateDesc");

            entity.HasIndex(e => new { e.CustomerId, e.CreateDate, e.MerchandiseOrderId })
                 .IsDescending(false, true, true)
                 .HasFilter("\"IsActive\" = TRUE")
                 .HasDatabaseName("IX_MO_Customer_Active_CreateDateDesc");

            // Cho bulk update/soft delete theo đơn
            entity.HasIndex(e => new { e.MerchandiseOrderId, e.IsActive })
                 .HasDatabaseName("IX_MO_Detail_Order_Active");

            // ===== Relationships =====
            entity.HasOne(d => d.AttachmentCollection)
                 .WithMany()
                 .HasForeignKey(d => d.AttachmentCollectionId)
                 .OnDelete(DeleteBehavior.Restrict)
                 .HasConstraintName("FK_MerchandiseOrders_AttachmentCollection");

            entity.HasOne(d => d.ComplaintReport)
                 .WithMany(p => p.ProcessingMerchandiseOrders)
                 .HasForeignKey(d => d.ComplaintReportId)
                 .OnDelete(DeleteBehavior.Restrict)
                 .HasConstraintName("FK_MerchandiseOrders_ComplaintReport");

            entity.HasOne(d => d.Company)
                 .WithMany(p => p.MerchandiseOrders)
                 .HasForeignKey(d => d.CompanyId)
                 .HasConstraintName("FK_MerchandiseOrders_Company");

            entity.HasOne(d => d.CreatedByNavigation)
                 .WithMany(p => p.MerchandiseOrderCreatedByNavigations)
                 .HasForeignKey(d => d.CreatedBy)
                 .HasConstraintName("FK_MerchandiseOrders_CreatedBy");

            entity.HasOne(d => d.Customer)
                 .WithMany(p => p.MerchandiseOrders)
                 .HasForeignKey(d => d.CustomerId)
                 .HasConstraintName("FK_MerchandiseOrders_Customer");

            entity.HasOne(d => d.ManagerBy)
                 .WithMany(p => p.MerchandiseOrderManagerBies)
                 .HasForeignKey(d => d.ManagerById)
                 .HasConstraintName("FK_MerchandiseOrders_ManagerById");

            entity.HasOne(d => d.DeliveryPausedByNavigation)
                 .WithMany(p => p.MerchandiseOrderDeliveryPausedBies)
                 .HasForeignKey(d => d.DeliveryPausedBy)
                 .OnDelete(DeleteBehavior.SetNull)
                 .HasConstraintName("FK_MerchandiseOrders_DeliveryPausedBy");

            entity.HasOne(d => d.UpdatedByNavigation)
                 .WithMany(p => p.MerchandiseOrderUpdatedByNavigations)
                 .HasForeignKey(d => d.UpdatedBy)
                 .HasConstraintName("FK_MerchandiseOrders_UpdatedBy");
        }
    }


}
