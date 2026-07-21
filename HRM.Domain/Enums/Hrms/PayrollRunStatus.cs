using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Hrms
{
    public enum PayrollRunStatus
    {
        Draft = 1,
        Calculated = 2,
        Locked = 3,
        Approved = 4,
        Paid = 5
    }
}
