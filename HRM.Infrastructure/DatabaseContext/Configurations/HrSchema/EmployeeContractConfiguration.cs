using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeContractConfiguration : IEntityTypeConfiguration<EmployeeContract>
    {
        public void Configure(EntityTypeBuilder<EmployeeContract> entity)
        {
            entity.ToTable("employee_contracts", "hr");

            entity.HasKey(e => e.EmployeeContractId).HasName("pk_employee_contracts");

            entity.Property(e => e.EmployeeContractId).HasColumnName("employee_contract_id").ValueGeneratedNever();
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.ContractNo).HasColumnName("contract_no").HasMaxLength(100);
            entity.Property(e => e.ContractType).HasColumnName("contract_type").IsRequired();
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsCurrent).HasColumnName("is_current").HasDefaultValue(false);

            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("ix_employee_contracts_employee_id");

            entity.HasOne(e => e.Employee)
                  .WithMany(e => e.EmployeeContracts)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_employee_contracts_employee");
        }
    }
}
