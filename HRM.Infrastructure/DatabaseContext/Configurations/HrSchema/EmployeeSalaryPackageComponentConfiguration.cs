using HRM.Domain.Entities.HrSchema.Salary_models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema;

public sealed class EmployeeSalaryPackageComponentConfiguration : IEntityTypeConfiguration<EmployeeSalaryPackageComponent>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryPackageComponent> entity)
    {
        entity.ToTable("EmployeeSalaryPackageComponents", "hr");
        entity.HasKey(x => x.EmployeeSalaryPackageComponentId);

        entity.Property(x => x.Amount).HasPrecision(18, 2);
        entity.Property(x => x.Rate).HasPrecision(18, 4);
        entity.Property(x => x.FormulaText).HasColumnType("text");
        entity.Property(x => x.Note).HasColumnType("text");

        entity.HasOne(x => x.EmployeeSalaryPackage)
            .WithMany(x => x.EmployeeSalaryPackageComponents)
            .HasForeignKey(x => x.EmployeeSalaryPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.SalaryComponentDefinition)
            .WithMany()
            .HasForeignKey(x => x.SalaryComponentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
