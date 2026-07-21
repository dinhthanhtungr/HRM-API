using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.CustomerEnum
{
    public enum CustomerWorkPlanStatus
    {
        Draft = 0,
        Active = 1,
        Paused = 2,
        Completed = 3,
        Canceled = 4
    }
}
