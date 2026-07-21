using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Hrms;

namespace HRM.Domain.Entities.HrSchema
{
    public class PayrollEmployeeRunDetail
    {
        public Guid PayrollEmployeeRunDetailId { get; set; } = Guid.CreateVersion7();

        public Guid PayrollEmployeeRunId { get; set; }
        public Guid SalaryComponentDefinitionId { get; set; }

        public string? ComponentCodeSnapshot { get; set; }
        public string? ComponentNameSnapshot { get; set; }

        public SalaryComponentCategory CategorySnapshot { get; set; }

        public decimal? Quantity { get; set; }
        public decimal? Rate { get; set; }
        public decimal Amount { get; set; }

        public PayrollSourceType SourceType { get; set; }
        public string? SourceReference { get; set; }

        public string? FormulaTextSnapshot { get; set; }
        public int SortOrder { get; set; }
        public string? Note { get; set; }

        public virtual PayrollEmployeeRun PayrollEmployeeRun { get; set; } = default!;
        public virtual SalaryComponentDefinition SalaryComponentDefinition { get; set; } = default!;
    }
}
