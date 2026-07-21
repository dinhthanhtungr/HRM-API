using HRM.Domain.Entities.HrSchema.Salary_models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema;

public sealed class EmployeeSalaryPackageConfiguration : IEntityTypeConfiguration<EmployeeSalaryPackage>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryPackage> entity)
    {
        entity.ToTable("EmployeeSalaryPackages", "hr");
        entity.HasKey(x => x.EmployeeSalaryPackageId);

        entity.Property(x => x.BasicSalary).HasPrecision(18, 2);
        entity.Property(x => x.InsuranceSalary).HasPrecision(18, 2);
        entity.Property(x => x.StandardWorkingDays).HasPrecision(18, 4);
        entity.Property(x => x.StandardWorkingHours).HasPrecision(18, 4);
        entity.Property(x => x.PaymentMethod).HasMaxLength(50);
        entity.Property(x => x.Note).HasColumnType("text");

        entity.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.BankAccount)
            .WithMany()
            .HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => new { x.EmployeeId, x.IsCurrent })
            .HasDatabaseName("IX_EmployeeSalaryPackages_Employee_Current");
    }
}
