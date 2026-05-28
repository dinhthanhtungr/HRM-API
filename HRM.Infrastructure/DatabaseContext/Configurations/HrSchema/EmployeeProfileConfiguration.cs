using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
    {
        public void Configure(EntityTypeBuilder<EmployeeProfile> entity)
        {
            entity.ToTable("employee_profiles", "hr");

            entity.HasKey(e => e.EmployeeProfileId)
                  .HasName("pk_employee_profiles");

            entity.Property(e => e.EmployeeProfileId)
                  .HasColumnName("employee_profile_id")
                  .ValueGeneratedNever();

            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");

            entity.Property(e => e.Ethnicity).HasColumnName("ethnicity").HasMaxLength(100);
            entity.Property(e => e.EducationLevel).HasColumnName("education_level");
            entity.Property(e => e.IdentifierIssueDate).HasColumnName("identifier_issue_date");
            entity.Property(e => e.IdentifierIssuePlace).HasColumnName("identifier_issue_place").HasMaxLength(255);
            entity.Property(e => e.PermanentAddress).HasColumnName("permanent_address").HasColumnType("text");
            entity.Property(e => e.TemporaryAddress).HasColumnName("temporary_address").HasColumnType("text");

            entity.HasIndex(e => e.EmployeeId)
                  .IsUnique()
                  .HasDatabaseName("ux_employee_profiles_employee_id");

            entity.HasOne(e => e.Employee)
                  .WithOne(e => e.EmployeeProfile)
                  .HasForeignKey<EmployeeProfile>(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_employee_profiles_employee");
        }
    }
}
