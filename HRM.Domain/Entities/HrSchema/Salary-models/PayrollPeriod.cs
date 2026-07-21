using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Hrms;

namespace HRM.Domain.Entities.HrSchema
{
    public class PayrollPeriod
    {
        public Guid PayrollPeriodId { get; set; } = Guid.CreateVersion7();

        // Mã kỳ lương để search nhanh, ví dụ: PAY-2026-04
        public string Code { get; set; } = default!;

        public int Year { get; set; }
        public int Month { get; set; }

        // Khoảng thời gian tính lương
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }

        public PayrollType PayrollType { get; set; } = PayrollType.Monthly;
        public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Draft;

        public string? Note { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }

        public virtual Employee? CreatedByNavigation { get; set; }
        public virtual Employee? ApprovedByNavigation { get; set; }

        public virtual ICollection<PayrollEmployeeRun> PayrollEmployeeRuns { get; set; } = new List<PayrollEmployeeRun>();
        public virtual ICollection<EmployeeInsuranceContribution> EmployeeInsuranceContributions { get; set; } = new List<EmployeeInsuranceContribution>();
    }
}
