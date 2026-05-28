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
    public class GroupConfiguration : IEntityTypeConfiguration<Group>
    {
        public void Configure(EntityTypeBuilder<Group> entity)
        {
            entity.HasKey(e => e.GroupId).HasName("PK__Groups__149AF36A3DC9A844");
            entity.ToTable("Groups", "company");

            entity.Property(e => e.GroupId)
                  .HasColumnName("GroupId")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.PartId).HasColumnName("PartId");
            entity.Property(e => e.GroupType).HasColumnName("GroupType");
            entity.Property(e => e.ExternalId).HasColumnName("ExternalId").HasColumnType("citext");
            entity.Property(e => e.Name).HasColumnName("Name").HasColumnType("citext");
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.UpdatedDate).HasColumnName("UpdatedDate");
            entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyId");

            entity.HasIndex(e => e.CompanyId).HasDatabaseName("IX_Groups_CompanyId");
            entity.HasIndex(e => e.CreatedBy).HasDatabaseName("IX_Groups_CreatedBy");
            entity.HasIndex(e => e.UpdatedBy).HasDatabaseName("IX_Groups_UpdatedBy");
            entity.HasIndex(e => e.PartId).HasDatabaseName("IX_Groups_PartId");
            entity.HasIndex(e => new { e.CompanyId, e.ExternalId })
                  .IsUnique()
                  .HasDatabaseName("UX_Groups_Company_ExternalId");

            entity.HasOne(d => d.Company).WithMany(p => p.Groups)
                  .HasForeignKey(d => d.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Groups_Company");

            // Tuỳ bạn: giữ Restrict để xóa Part không ảnh hưởng Group (an toàn)
            entity.HasOne(d => d.Part).WithMany(p => p.Groups)
                  .HasForeignKey(d => d.PartId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Groups_Part");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.GroupCreatedByNavigations)
                  .HasForeignKey(d => d.CreatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Groups_CreatedBy");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.GroupUpdatedByNavigations)
                  .HasForeignKey(d => d.UpdatedBy)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_Groups_UpdatedBy");
        }
    }
}
