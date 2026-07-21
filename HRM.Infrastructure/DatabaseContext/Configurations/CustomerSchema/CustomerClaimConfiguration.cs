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
    public class CustomerClaimConfiguration : IEntityTypeConfiguration<CustomerClaim>
    {
        public void Configure(EntityTypeBuilder<CustomerClaim> entity)
        {
            entity.ToTable("CustomerClaims", "Customer");

            entity.HasKey(x => x.Id)
                  .HasName("PK_CustomerClaims_Id");

            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.CustomerId)
                  .HasColumnName("customerId")
                  .IsRequired();

            entity.Property(x => x.EmployeeId)
                  .HasColumnName("employeeId")
                  .IsRequired();

            entity.Property(x => x.GroupId)
                  .HasColumnName("groupId")
                  .IsRequired();

            entity.Property(x => x.Type)
                  .HasColumnName("type")          // enum → int
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(x => x.ExpiresAt)
                  .HasColumnName("expiresAt")
                  .IsRequired();

            entity.Property(x => x.IsActive)
                  .HasColumnName("isActive")
                  .HasDefaultValue(true)
                  .IsRequired();

            entity.Property(x => x.CompanyId)
                  .HasColumnName("companyId")
                  .IsRequired();

            // Indexes
            entity.HasIndex(x => new { x.CompanyId, x.CustomerId })
                  .HasDatabaseName("ix_customerclaims_company_customer");

            entity.HasIndex(x => x.ExpiresAt)
                  .HasDatabaseName("ix_customerclaims_expiresat");


            entity.HasIndex(x => new { x.CompanyId, x.ExpiresAt })
                  .HasFilter(@"""isActive"" = TRUE AND ""type"" = 1")
                  .HasDatabaseName("ix_customerclaims_active_by_company");

            entity.HasIndex(x => new { x.CustomerId, x.ExpiresAt })
                  .HasFilter(@"""isActive"" = TRUE AND ""type"" = 1")
                  .HasDatabaseName("ix_customerclaims_by_customer_active");

            entity.HasIndex(x => new { x.CompanyId, x.GroupId, x.ExpiresAt })
                  .HasFilter(@"""isActive"" = TRUE AND ""type"" = 1")
                  .HasDatabaseName("ix_customerclaims_by_group_active");


            // Unique: 1 claim đang hoạt động / (Customer, Employee, Group, Type)
            entity.HasIndex(x => new { x.CustomerId, x.EmployeeId, x.GroupId, x.Type })
                  .IsUnique()
                  .HasFilter(@"""isActive"" = TRUE")
                  .HasDatabaseName("ux_customerclaims_active_unique");

            // FK
            entity.HasOne(x => x.Customer)
                  .WithMany(c => c.CustomerClaims)
                  .HasForeignKey(x => x.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_CustomerClaims_Customer");

            entity.HasOne(x => x.Employee)
                  .WithMany(e => e.CustomerClaims)
                  .HasForeignKey(x => x.EmployeeId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerClaims_Employee");

            entity.HasOne(x => x.Group)
                  .WithMany(g => g.CustomerClaims)
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerClaims_Group");

            entity.HasOne(x => x.Company)
                  .WithMany(c => c.CustomerClaims)
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerClaims_Company");
        }
    }
}
