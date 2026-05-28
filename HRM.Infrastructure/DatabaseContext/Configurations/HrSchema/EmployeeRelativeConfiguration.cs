using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeRelativeConfiguration : IEntityTypeConfiguration<EmployeeRelative>
    {
        public void Configure(EntityTypeBuilder<EmployeeRelative> entity)
        {
            entity.ToTable("employee_relatives", "hr");

            entity.HasKey(e => e.EmployeeRelativeId).HasName("pk_employee_relatives");

            entity.Property(e => e.EmployeeRelativeId).HasColumnName("employee_relative_id").ValueGeneratedNever();
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Relationship).HasColumnName("relationship");
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(50);
            entity.Property(e => e.IsEmergencyContact).HasColumnName("is_emergency_contact").HasDefaultValue(false);

            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("ix_employee_relatives_employee_id");

            entity.HasOne(e => e.Employee)
                  .WithMany(e => e.EmployeeRelatives)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_employee_relatives_employee");
        }
    }
}
