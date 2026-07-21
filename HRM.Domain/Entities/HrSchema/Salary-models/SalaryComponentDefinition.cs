using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Hrms;

namespace HRM.Domain.Entities.HrSchema
{
    public class SalaryComponentDefinition
    {
        public Guid SalaryComponentDefinitionId { get; set; } = Guid.CreateVersion7();

        // Ví dụ: BASIC, BHXH_EMP, PIT, OT_WEEKEND
        public string Code { get; set; } = default!;

        public string Name { get; set; } = default!;

        public SalaryComponentCategory Category { get; set; }
        public SalaryComponentValueType ValueType { get; set; }

        public bool AffectsGross { get; set; }
        public bool AffectsNet { get; set; }
        public bool AffectsInsuranceBase { get; set; }
        public bool AffectsTaxableIncome { get; set; }

        public int SortOrder { get; set; }
        public bool IsSystem { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public string? FormulaTemplate { get; set; }
        public string? Note { get; set; }

        public virtual ICollection<PayrollEmployeeRunDetail> PayrollEmployeeRunDetails { get; set; } = new List<PayrollEmployeeRunDetail>();
    }
}
