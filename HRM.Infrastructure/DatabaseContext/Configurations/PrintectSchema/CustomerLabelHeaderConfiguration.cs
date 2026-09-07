using HRM.Domain.Entities.PrintectSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace HRM.Infrastructure.DatabaseContext.Configurations.PrintectSchema
{
    public class CustomerLabelHeaderConfiguration : IEntityTypeConfiguration<CustomerLabelHeader>
    {
        public void Configure(EntityTypeBuilder<CustomerLabelHeader> entity)
        {
            entity.ToTable("CustomerLabelHeaders", "printect");

            entity.HasKey(x => x.Id).HasName("PK_CustomerLabelHeaders_Id");

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.ProductId).HasColumnName("productId").IsRequired();
            entity.Property(x => x.ColorCode).HasColumnName("colorCode").HasColumnType("citext");
            entity.Property(x => x.CustomerId).HasColumnName("customerId").IsRequired();
            entity.Property(x => x.CustomerExternalId).HasColumnName("customerExternalId").HasColumnType("citext");
            entity.Property(x => x.LabelType).HasColumnName("labelType").HasColumnType("citext");
            entity.Property(x => x.CreatedBy).HasColumnName("createdBy");
            entity.Property(x => x.UpdatedBy).HasColumnName("updatedBy");
            entity.Property(x => x.IsActive).HasColumnName("isActive").HasDefaultValue(true);
            entity.Property(x => x.CreatedDate).HasColumnName("createdDate");
            entity.Property(x => x.UpdatedDate).HasColumnName("updatedDate");

            entity.HasIndex(x => x.ProductId).HasDatabaseName("IX_CustomerLabelHeaders_ProductId");
            entity.HasIndex(x => x.CustomerId).HasDatabaseName("IX_CustomerLabelHeaders_CustomerId");
            entity.HasIndex(x => x.LabelType).HasDatabaseName("IX_CustomerLabelHeaders_LabelType");
            entity.HasIndex(x => new { x.CustomerId, x.ProductId, x.LabelType })
                  .HasDatabaseName("IX_CustomerLabelHeaders_Customer_Product_LabelType");

            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_CustomerLabelHeaders_Product");

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_CustomerLabelHeaders_Customer");

            entity.HasOne(x => x.CreatedByNavigation)
                .WithMany()
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CustomerLabelHeaders_CreatedBy");

            entity.HasOne(x => x.UpdatedByNavigation)
                .WithMany()
                .HasForeignKey(x => x.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CustomerLabelHeaders_UpdatedBy");
        }
    }
}
