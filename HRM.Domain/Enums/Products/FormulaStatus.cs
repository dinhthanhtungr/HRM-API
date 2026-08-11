using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Products
{
    public enum FormulaStatus
    {
        Unknown = 0,
        Draft = 1,
        Approved = 2,
        SampleSent = 3,
        Cancelled = 4,
        Completed = 5,
        PendingSaleConfirmation = 6,
        Rejected = 7
    }
}
