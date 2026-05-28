using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CompanySchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CompanySchema
{
    public class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> entity)
        {
            entity.HasKey(e => e.CompanyId).HasName("PK__Companie__2D971CAC11C45530");
            entity.ToTable("Companies", "company");

            entity.HasIndex(e => e.CreatedBy).HasDatabaseName("IX_Companies_CreatedBy");
            entity.HasIndex(e => e.UpdatedBy).HasDatabaseName("IX_Companies_UpdatedBy");

            entity.Property(e => e.CompanyId)
                  .HasColumnName("companyId")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Code).HasColumnName("companyExternalId").HasColumnType("citext").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(e => e.Address).HasColumnName("address").HasColumnType("text");
            entity.Property(e => e.Country).HasColumnName("country").HasColumnType("text");
            entity.Property(e => e.ZipCode).HasColumnName("zipCode").HasColumnType("text");
            entity.Property(e => e.Phone).HasColumnName("phone").HasColumnType("text");
            entity.Property(e => e.Email).HasColumnName("email").HasColumnType("text");
            entity.Property(e => e.IsActive).HasColumnName("isActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedDate).HasColumnName("createdDate");
            entity.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("createdBy");
            entity.Property(e => e.UpdatedBy).HasColumnName("updatedBy");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CompanyCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Companies_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CompanyUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Companies_UpdatedBy");
        }
    }
}
