using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HrSchema.Salary_models
{
    public class EmployeeSalaryPackageComponent
    {
        public Guid EmployeeSalaryPackageComponentId { get; set; } = Guid.CreateVersion7();

        public Guid EmployeeSalaryPackageId { get; set; }
        public Guid SalaryComponentDefinitionId { get; set; }

        public decimal? Amount { get; set; }
        public decimal? Rate { get; set; }
        public string? FormulaText { get; set; }

        public bool IsRecurring { get; set; } = true;
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }

        public string? Note { get; set; }

        public virtual EmployeeSalaryPackage EmployeeSalaryPackage { get; set; } = default!;
        public virtual SalaryComponentDefinition SalaryComponentDefinition { get; set; } = default!;
    }
}
