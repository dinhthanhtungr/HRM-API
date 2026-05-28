using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class EmployeeBankAccountConfiguration : IEntityTypeConfiguration<EmployeeBankAccount>
    {
        public void Configure(EntityTypeBuilder<EmployeeBankAccount> entity)
        {
            entity.ToTable("employee_bank_accounts", "hr");

            entity.HasKey(e => e.EmployeeBankAccountId).HasName("pk_employee_bank_accounts");

            entity.Property(e => e.EmployeeBankAccountId).HasColumnName("employee_bank_account_id").ValueGeneratedNever();
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.BankName).HasColumnName("bank_name").HasMaxLength(255);
            entity.Property(e => e.AccountNumber).HasColumnName("account_number").HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccountHolder).HasColumnName("account_holder").HasMaxLength(255);
            entity.Property(e => e.IsPayrollAccount).HasColumnName("is_payroll_account").HasDefaultValue(false);

            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("ix_employee_bank_accounts_employee_id");

            entity.HasOne(e => e.Employee)
                  .WithMany(e => e.EmployeeBankAccounts)
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_employee_bank_accounts_employee");
        }
    }
}
