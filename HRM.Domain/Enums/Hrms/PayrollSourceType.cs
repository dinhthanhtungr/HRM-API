using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Hrms
{
    public enum PayrollSourceType
    {
        Manual = 1,
        Attendance = 2,
        SalaryPackage = 3,
        Insurance = 4,
        Tax = 5,
        Imported = 6,
        Formula = 7
    }
}
