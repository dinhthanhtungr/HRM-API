using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.CustomerEnum
{
    public enum CustomerFollowUpTaskStatus
    {
        Pending = 0,
        InProgress = 1,
        Done = 2,
        Canceled = 3,
        Overdue = 4
    }
}


