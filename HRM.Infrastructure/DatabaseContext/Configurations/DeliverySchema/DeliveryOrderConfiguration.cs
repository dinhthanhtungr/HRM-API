using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryOrderConfiguration : IEntityTypeConfiguration<DeliveryOrder>
    {
        public void Configure(EntityTypeBuilder<DeliveryOrder> entity)
        {
            entity.ToTable("DeliveryOrders", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK__DeliveryOrder__A2F6B5D8D1C1E3E3");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.Property(e => e.Status)
                  .IsRequired()
                  .HasColumnName("Status");

            entity.Property(e => e.ExternalId)
                  .HasColumnType("citext")
                  .HasColumnName("ExternalId");

            entity.Property(e => e.CustomerExternalIdSnapShot)
                  .HasColumnType("citext")
                  .HasColumnName("CustomerExternalIdSnapShot");

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true)
                  .HasColumnName("IsActive");

            entity.Property(e => e.RequiresUnloading)
                  .HasColumnName("RequiresUnloading");

            entity.Property(e => e.HasPrinted)
                  .HasDefaultValue(false)
                  .HasColumnName("HasPrinted");

            entity.Property(e => e.DeliveryPrice)
                  .HasColumnType("numeric(18,2)")
                  .HasColumnName("DeliveryPrice");

            entity.Property(e => e.CreatedDate)
                  .HasColumnName("CreatedDate");

            entity.Property(e => e.UpdatedDate)
                  .HasColumnName("UpdatedDate");

            entity.HasIndex(e => e.CompanyId, "IX_DeliveryOrders_CompanyId");
            entity.HasIndex(e => e.CreatedBy, "IX_DeliveryOrders_CreatedBy");
            entity.HasIndex(e => e.UpdatedBy, "IX_DeliveryOrders_UpdatedBy");
            entity.HasIndex(e => e.CustomerId, "IX_DeliveryOrders_CustomerId");

            entity.HasIndex(e => e.ExternalId, "UX_DeliveryOrders_ExternalId")
                .IsUnique();

            entity.HasOne(d => d.Customer)
                  .WithMany()
                  .HasForeignKey(d => d.CustomerId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_DeliveryOrder_Customer");

            entity.HasOne(d => d.Company)
                  .WithMany()
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_DeliveryOrder_Company");

            entity.HasOne(d => d.CreatedByNavigation)
                  .WithMany(d => d.DeliveryOrderCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_DeliveryOrder_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation)
                  .WithMany(d => d.DeliveryOrderUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_DeliveryOrder_UpdatedBy");
        }
    }
}
