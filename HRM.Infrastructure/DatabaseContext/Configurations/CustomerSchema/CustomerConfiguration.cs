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
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> entity)
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__A4AE64D875977EBB");
            entity.ToTable("Customer", "Customer");

            entity.Property(e => e.CustomerId)
                  .HasColumnName("CustomerId")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.ExternalId).HasColumnName("ExternalId").HasColumnType("citext").IsRequired();
            entity.Property(e => e.CustomerName).HasColumnName("CustomerName").HasColumnType("citext").IsRequired();
            entity.Property(e => e.CustomerGroup).HasColumnName("CustomerGroup").HasColumnType("citext");
            entity.Property(e => e.ApplicationName).HasColumnName("ApplicationName").HasColumnType("citext");
            entity.Property(e => e.RegistrationNumber).HasColumnName("RegistrationNumber").HasColumnType("citext");
            entity.Property(e => e.RegistrationAddress).HasColumnName("RegistrationAddress").HasColumnType("citext");
            entity.Property(e => e.TaxNumber).HasColumnName("TaxNumber").HasColumnType("citext");
            entity.Property(e => e.Phone).HasColumnName("Phone").HasColumnType("citext");
            entity.Property(e => e.Website).HasColumnName("Website").HasColumnType("citext");
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.UpdatedDate).HasColumnName("UpdatedDate");
            entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId");
            entity.Property(e => e.IssueDate).HasColumnName("IssueDate");
            entity.Property(e => e.IssuedPlace).HasColumnName("IssuedPlace").HasColumnType("citext");
            entity.Property(e => e.FaxNumber).HasColumnName("FaxNumber").HasColumnType("citext");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

            entity.Property(e => e.IsLead).HasColumnName("IsLead").HasDefaultValue(true);
            entity.Property(e => e.LeadStatus)
                  .HasColumnName("LeadStatus")
                  .HasConversion<int>()                           // lưu dưới DB là int
                  .HasDefaultValue(LeadStatus.Open);              // default bằng enum (OK)


            entity.Property(e => e.LastContactDate)
                  .HasColumnName("LastContactDate");

            entity.Property(e => e.NextFollowUpDate)
                  .HasColumnName("NextFollowUpDate");

            entity.Property(e => e.CurrentCrmStatus)
                  .HasColumnName("CurrentCrmStatus")
                  .HasColumnType("citext");

            entity.Property(e => e.CurrentSaleId)
                  .HasColumnName("CurrentSaleId");

            entity.HasIndex(e => e.CompanyId).HasDatabaseName("IX_Customers_CompanyId");
            entity.HasIndex(e => e.CreatedBy).HasDatabaseName("IX_Customers_CreatedBy");
            entity.HasIndex(e => e.UpdatedBy).HasDatabaseName("IX_Customers_UpdatedBy");

            entity.HasIndex(e => new { e.CompanyId, e.ExternalId })
                  .IsUnique()
                  .HasDatabaseName("UX_Customers_Company_ExternalId");

            entity.HasIndex(e => new { e.CompanyId, e.IsLead, e.LeadStatus, e.CreatedDate })
                  .IsDescending(false, false, false, true)
                  .HasDatabaseName("ix_customer_company_islead_status_created_desc");

            entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.CreatedDate, e.CustomerId })
                  .IsDescending(false, false, true, true)
                  .HasDatabaseName("IX_Customers_Company_IsActive_CreatedDateDesc");

            entity.HasIndex(e => new { e.CompanyId, e.CurrentSaleId, e.NextFollowUpDate })
                  .HasDatabaseName("IX_Customers_Company_CurrentSale_NextFollowUp");

            entity.HasIndex(e => new { e.CompanyId, e.CurrentCrmStatus, e.NextFollowUpDate })
                  .HasDatabaseName("IX_Customers_Company_CrmStatus_NextFollowUp");

            // KHÔNG cascade với Company/User
            entity.HasOne(d => d.Company).WithMany(p => p.Customers)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Customers_Company");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Customers_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Customers_UpdatedBy");
        }
    }
}
