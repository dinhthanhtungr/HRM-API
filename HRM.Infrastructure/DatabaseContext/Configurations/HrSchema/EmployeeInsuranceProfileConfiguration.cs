using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeInsuranceProfileConfiguration : IEntityTypeConfiguration<EmployeeInsuranceProfile>
    {
        public void Configure(EntityTypeBuilder<EmployeeInsuranceProfile> entity)
        {
            entity.ToTable("employee_insurance_profiles", "hr");

            entity.HasKey(e => e.EmployeeInsuranceProfileId).HasName("pk_employee_insurance_profiles");

            entity.Property(e => e.EmployeeInsuranceProfileId).HasColumnName("employee_insurance_profile_id").ValueGeneratedNever();
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.SocialInsuranceNumber).HasColumnName("social_insurance_number").HasMaxLength(100);
            entity.Property(e => e.TaxCode).HasColumnName("tax_code").HasMaxLength(100);
            entity.Property(e => e.HealthInsuranceNumber).HasColumnName("health_insurance_number").HasMaxLength(100);

            entity.HasIndex(e => e.EmployeeId)
                  .IsUnique()
                  .HasDatabaseName("ux_employee_insurance_profiles_employee_id");

            entity.HasOne(e => e.Employee)
                  .WithMany(e => e.EmployeeInsuranceProfiles)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_employee_insurance_profiles_employee");
        }
    }
}
