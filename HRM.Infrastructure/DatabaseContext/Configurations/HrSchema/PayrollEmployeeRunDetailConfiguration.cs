using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema;

public sealed class PayrollEmployeeRunDetailConfiguration : IEntityTypeConfiguration<PayrollEmployeeRunDetail>
{
    public void Configure(EntityTypeBuilder<PayrollEmployeeRunDetail> entity)
    {
        entity.ToTable("PayrollEmployeeRunDetails", "hr");
        entity.HasKey(x => x.PayrollEmployeeRunDetailId);

        entity.Property(x => x.ComponentCodeSnapshot).HasMaxLength(50);
        entity.Property(x => x.ComponentNameSnapshot).HasMaxLength(255);
        entity.Property(x => x.FormulaTextSnapshot).HasColumnType("text");
        entity.Property(x => x.Note).HasColumnType("text");

        entity.Property(x => x.Quantity).HasPrecision(18, 4);
        entity.Property(x => x.Rate).HasPrecision(18, 4);
        entity.Property(x => x.Amount).HasPrecision(18, 2);

        entity.HasOne(x => x.PayrollEmployeeRun)
            .WithMany(x => x.PayrollEmployeeRunDetails)
            .HasForeignKey(x => x.PayrollEmployeeRunId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.SalaryComponentDefinition)
            .WithMany(x => x.PayrollEmployeeRunDetails)
            .HasForeignKey(x => x.SalaryComponentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
