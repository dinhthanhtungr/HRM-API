using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Hrms;

namespace HRM.Domain.Entities.HrSchema
{
    public class PayrollEmployeeRun
    {
        public Guid PayrollEmployeeRunId { get; set; } = Guid.CreateVersion7();

        public Guid PayrollPeriodId { get; set; }
        public Guid EmployeeId { get; set; }

        // Snapshot để tránh bị đổi dữ liệu lịch sử
        public string? EmployeeCodeSnapshot { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        public string? DepartmentSnapshot { get; set; }
        public string? JobTitleSnapshot { get; set; }
        public string? BankAccountSnapshot { get; set; }

        public decimal? BaseSalarySnapshot { get; set; }
        public decimal? InsuranceSalarySnapshot { get; set; }

        public decimal GrossIncome { get; set; }
        public decimal TotalEarnings { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal EmployerContributionTotal { get; set; }
        public decimal NetIncome { get; set; }

        public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
        public string? Note { get; set; }

        public Guid? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }

        public virtual PayrollPeriod PayrollPeriod { get; set; } = default!;
        public virtual Employee Employee { get; set; } = default!;
        public virtual Employee? LockedByNavigation { get; set; }

        public virtual ICollection<PayrollEmployeeRunDetail> PayrollEmployeeRunDetails { get; set; } = new List<PayrollEmployeeRunDetail>();
    }
}
