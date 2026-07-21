using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema;

public sealed class PayrollEmployeeRunConfiguration : IEntityTypeConfiguration<PayrollEmployeeRun>
{
    public void Configure(EntityTypeBuilder<PayrollEmployeeRun> entity)
    {
        entity.ToTable("PayrollEmployeeRuns", "hr");
        entity.HasKey(x => x.PayrollEmployeeRunId);

        entity.Property(x => x.EmployeeCodeSnapshot).HasMaxLength(50);
        entity.Property(x => x.EmployeeNameSnapshot).HasMaxLength(255);
        entity.Property(x => x.DepartmentSnapshot).HasMaxLength(255);
        entity.Property(x => x.JobTitleSnapshot).HasMaxLength(255);
        entity.Property(x => x.BankAccountSnapshot).HasMaxLength(255);
        entity.Property(x => x.Note).HasColumnType("text");

        entity.Property(x => x.BaseSalarySnapshot).HasPrecision(18, 2);
        entity.Property(x => x.InsuranceSalarySnapshot).HasPrecision(18, 2);
        entity.Property(x => x.GrossIncome).HasPrecision(18, 2);
        entity.Property(x => x.TotalEarnings).HasPrecision(18, 2);
        entity.Property(x => x.TotalDeductions).HasPrecision(18, 2);
        entity.Property(x => x.EmployerContributionTotal).HasPrecision(18, 2);
        entity.Property(x => x.NetIncome).HasPrecision(18, 2);

        entity.HasOne(x => x.PayrollPeriod)
            .WithMany(x => x.PayrollEmployeeRuns)
            .HasForeignKey(x => x.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Employee)
            .WithMany(x => x.PayrollEmployeeRuns)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.LockedByNavigation)
            .WithMany()
            .HasForeignKey(x => x.LockedBy)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => new { x.PayrollPeriodId, x.EmployeeId })
            .IsUnique()
            .HasDatabaseName("UX_PayrollEmployeeRuns_Period_Employee");
    }
}
