using HRM.Domain.Entities.HrSchema.Hrm_models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HrSchema.Salary_models
{
    public class EmployeeSalaryPackage
    {
        public Guid EmployeeSalaryPackageId { get; set; } = Guid.CreateVersion7();

        public Guid EmployeeId { get; set; }

        public decimal BasicSalary { get; set; }
        public decimal? InsuranceSalary { get; set; }

        public decimal? StandardWorkingDays { get; set; }
        public decimal? StandardWorkingHours { get; set; }

        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public bool IsCurrent { get; set; } = true;

        public string? PaymentMethod { get; set; }
        public Guid? BankAccountId { get; set; }

        public string? Note { get; set; }

        public virtual Employee Employee { get; set; } = default!;
        public virtual EmployeeBankAccount? BankAccount { get; set; }

        public virtual ICollection<EmployeeSalaryPackageComponent> EmployeeSalaryPackageComponents { get; set; } = new List<EmployeeSalaryPackageComponent>();
    }
}
