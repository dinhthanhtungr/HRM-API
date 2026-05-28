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
    public class MemberInGroupConfiguration : IEntityTypeConfiguration<MemberInGroup>
    {
        public void Configure(EntityTypeBuilder<MemberInGroup> entity)
        {
            entity.HasKey(e => e.MemberId).HasName("PK__MemberIn__0CF04B189ED315D5");
            entity.ToTable("MemberInGroup", "company");

            entity.Property(e => e.MemberId)
                  .HasColumnName("MemberId")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.IsAdmin).HasColumnName("IsAdmin").HasDefaultValue(false);
            entity.Property(e => e.Profile).HasColumnName("Profile"); // EmployeeId (nullable)
            entity.Property(e => e.GroupId).HasColumnName("GroupId"); // <-- NÊN là Guid (non-null)
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

            entity.HasIndex(e => e.GroupId).HasDatabaseName("IX_MemberInGroup_GroupId");
            entity.HasIndex(e => e.Profile).HasDatabaseName("IX_MemberInGroup_Profile");
            entity.HasIndex(e => new { e.GroupId, e.Profile })
                  .IsUnique()
                  .HasFilter("\"IsActive\" = TRUE")
                  .HasDatabaseName("UX_MemberInGroup_Group_Profile_Active");

            // XÓA GROUP -> XÓA TẤT CẢ THÀNH VIÊN
            entity.HasOne(d => d.Group).WithMany(p => p.MemberInGroups)
                  .HasForeignKey(d => d.GroupId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_MemberInGroup_Group");

            // Xoá employee -> giữ record, set null Profile
            entity.HasOne(d => d.ProfileNavigation).WithMany(p => p.MemberInGroups)
                  .HasForeignKey(d => d.Profile)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_MemberInGroup_Profile");
        }
    }
}
