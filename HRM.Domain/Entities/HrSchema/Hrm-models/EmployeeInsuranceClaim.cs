using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeInsuranceClaim
    {
        public Guid EmployeeInsuranceClaimId { get; set; } = Guid.CreateVersion7();

        public Guid EmployeeId { get; set; }

        public DateOnly? ClaimMonth { get; set; }
        public string? ClaimMonthLabel { get; set; }
        public int? SequenceNo { get; set; }

        public string? EmployeeNameSnapshot { get; set; }
        public string? SocialInsuranceNumberSnapshot { get; set; }

        public string? LeaveReason { get; set; }
        public DateOnly? LeaveFromDate { get; set; }
        public DateOnly? LeaveToDate { get; set; }
        public decimal? LeaveDays { get; set; }

        public string? ClaimType { get; set; }
        public string? ClaimNumber { get; set; }
        public string? ProcessingStatus { get; set; }

        public DateOnly? AppointmentDate { get; set; }
        public DateOnly? ReturnedToEmployeeDate { get; set; }

        public string? Note { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }
}
