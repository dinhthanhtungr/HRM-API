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
    public class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
    {
        public void Configure(EntityTypeBuilder<CustomerNote> entity)
        {
            entity.ToTable("CustomerNotes", "Customer");

            entity.HasKey(x => x.Id)
                  .HasName("PK_CustomerNotes_Id");

            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(x => x.CustomerId)
                  .HasColumnName("customerId")
                  .IsRequired();

            entity.Property(x => x.AuthorEmployeeId)
                  .HasColumnName("authorEmployeeId")
                  .IsRequired();

            entity.Property(x => x.AuthorGroupId)
                  .HasColumnName("authorGroupId")
                  .IsRequired();

            entity.Property(x => x.Content)
                  .HasColumnName("content")
                  .IsRequired();

            entity.Property(x => x.Visibility)
                  .HasColumnName("visibility")        // enum → int
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(x => x.IsApprovedShare)
                  .HasColumnName("isApprovedShare")
                  .HasDefaultValue(false);

            entity.Property(x => x.CreatedAt)
                  .HasColumnName("createdAt");

            entity.Property(x => x.CompanyId)
                  .HasColumnName("companyId")
                  .IsRequired();

            // Indexes “thực dụng”
            entity.HasIndex(x => new { x.CompanyId, x.CustomerId, x.CreatedAt })
                  .HasDatabaseName("ix_customernotes_company_customer_createdat");

            // (Tuỳ chọn) index lọc cho các ghi chú đã duyệt share
            entity.HasIndex(x => new { x.CompanyId, x.CustomerId })
                  .HasFilter(@"""isApprovedShare"" = TRUE")
                  .HasDatabaseName("ix_customernotes_public");

            // FK
            entity.HasOne(x => x.Customer)
                  .WithMany(c => c.CustomerNotes)
                  .HasForeignKey(x => x.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_CustomerNotes_Customer");

            entity.HasOne(x => x.AuthorEmployee)
                  .WithMany(e => e.CustomerNotesAuthored)
                  .HasForeignKey(x => x.AuthorEmployeeId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerNotes_AuthorEmployee");

            entity.HasOne(x => x.AuthorGroup)
                  .WithMany(g => g.CustomerNotes)
                  .HasForeignKey(x => x.AuthorGroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerNotes_AuthorGroup");

            entity.HasOne(x => x.Company)
                  .WithMany(c => c.CustomerNotes)
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_CustomerNotes_Company");
        }
    }
}
