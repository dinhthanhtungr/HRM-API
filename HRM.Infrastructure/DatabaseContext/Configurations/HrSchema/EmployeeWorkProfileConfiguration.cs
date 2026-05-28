using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeWorkProfileConfiguration : IEntityTypeConfiguration<EmployeeWorkProfile>
    {
        public void Configure(EntityTypeBuilder<EmployeeWorkProfile> entity)
        {
            entity.ToTable("employee_work_profiles", "hr");

            entity.HasKey(e => e.EmployeeWorkProfileId)
                  .HasName("pk_employee_work_profiles");

            entity.Property(e => e.EmployeeWorkProfileId)
                  .HasColumnName("employee_work_profile_id")
                  .ValueGeneratedNever();

            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.AttendanceCode).HasColumnName("attendance_code").HasMaxLength(64);

            entity.Property(e => e.PartId).HasColumnName("part_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.JobTitleId).HasColumnName("job_title_id");

            entity.Property(e => e.WorkLocation).HasColumnName("work_location").HasMaxLength(255);

            entity.Property(e => e.ProbationEndDate).HasColumnName("probation_end_date");
            entity.Property(e => e.OnboardingTrainingDate).HasColumnName("onboarding_training_date");

            entity.Property(e => e.IsCurrent).HasColumnName("is_current").HasDefaultValue(true);
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedDate).HasColumnName("created_date").HasDefaultValueSql("now()");

            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("ix_employee_work_profiles_employee_id");
            entity.HasIndex(e => e.PartId).HasDatabaseName("ix_employee_work_profiles_part_id");
            entity.HasIndex(e => e.GroupId).HasDatabaseName("ix_employee_work_profiles_group_id");
            entity.HasIndex(e => e.JobTitleId).HasDatabaseName("ix_employee_work_profiles_job_title_id");

            entity.HasIndex(e => new { e.EmployeeId, e.IsCurrent })
                  .IsUnique()
                  .HasFilter("\"is_current\" = true")
                  .HasDatabaseName("ux_employee_work_profiles_one_current");

            entity.HasOne(e => e.Employee)
                  .WithMany(e => e.EmployeeWorkProfiles)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_employee_work_profiles_employee");

            entity.HasOne(e => e.Part)
                  .WithMany()
                  .HasForeignKey(e => e.PartId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_employee_work_profiles_part");

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_employee_work_profiles_group");

            entity.HasOne(e => e.JobTitle)
                  .WithMany(e => e.EmployeeWorkProfiles)
                  .HasForeignKey(e => e.JobTitleId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("fk_employee_work_profiles_job_title");


            entity.HasOne(e => e.CreatedByNavigation)
                  .WithMany(e => e.EmployeeWorkProfileCreatedByNavigations)
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_employee_work_profiles_created_by");
        }
    }
}
