using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.CustomerEnum
{
    public enum CustomerInteractionType
    {
        Call = 0,
        Meeting = 1,
        Email = 2,
        Zalo = 3,
        Visit = 4,
        Other = 99
    }
}
