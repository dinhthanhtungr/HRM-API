using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema
{
    public class CustomerInteractionConfiguration : IEntityTypeConfiguration<CustomerInteraction>
    {
        public void Configure(EntityTypeBuilder<CustomerInteraction> entity)
        {
            entity.HasKey(e => e.Id).HasName("PK_CustomerInteractions_Id");
            entity.ToTable("CustomerInteractions", "Customer");

            entity.Property(e => e.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerId").IsRequired();
            entity.Property(e => e.ContactId).HasColumnName("ContactId");
            entity.Property(e => e.InteractionType).HasColumnName("InteractionType").HasConversion<int>().HasDefaultValue(CustomerInteractionType.Call);
            entity.Property(e => e.Subject).HasColumnName("Subject").HasColumnType("citext");
            entity.Property(e => e.Content).HasColumnName("Content").HasColumnType("text").IsRequired();
            entity.Property(e => e.Outcome).HasColumnName("Outcome").HasColumnType("text");
            entity.Property(e => e.NextAction).HasColumnName("NextAction").HasColumnType("text");
            entity.Property(e => e.InteractionAt).HasColumnName("InteractionAt");
            entity.Property(e => e.NextFollowUpDate).HasColumnName("NextFollowUpDate");
            entity.Property(e => e.AssignedSaleEmployeeId).HasColumnName("AssignedSaleEmployeeId");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId").IsRequired();
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.UpdatedDate).HasColumnName("UpdatedDate");
            entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

            entity.HasIndex(e => new { e.CompanyId, e.CustomerId, e.InteractionAt })
                  .IsDescending(false, false, true)
                  .HasDatabaseName("IX_CustomerInteractions_Company_Customer_InteractionAtDesc");

            entity.HasIndex(e => new { e.CompanyId, e.AssignedSaleEmployeeId, e.NextFollowUpDate })
                  .HasDatabaseName("IX_CustomerInteractions_Company_Assigned_NextFollowUp");

            entity.HasOne(e => e.Customer).WithMany(c => c.CustomerInteractions)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_CustomerInteractions_Customer");

            entity.HasOne(e => e.Contact).WithMany()
                  .HasForeignKey(e => e.ContactId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerInteractions_Contact");

            entity.HasOne(e => e.Company).WithMany()
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerInteractions_Company");

            entity.HasOne(e => e.AssignedSaleEmployee).WithMany()
                  .HasForeignKey(e => e.AssignedSaleEmployeeId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerInteractions_AssignedSaleEmployee");

            entity.HasOne(e => e.CreatedByNavigation).WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerInteractions_CreatedBy");

            entity.HasOne(e => e.UpdatedByNavigation).WithMany()
                  .HasForeignKey(e => e.UpdatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_CustomerInteractions_UpdatedBy");
        }
    }
}
