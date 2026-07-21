using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HrSchema.Hrm_models
{
    public class EmployeeBankAccount
    {
        public Guid EmployeeBankAccountId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public string? BankName { get; set; }
        public string AccountNumber { get; set; } = default!;
        public string? AccountHolder { get; set; }
        public bool IsPayrollAccount { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }

}
