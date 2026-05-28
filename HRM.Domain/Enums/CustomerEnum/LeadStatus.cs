using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.CustomerEnum
{
    public enum LeadStatus 
    { 
        Open = 0, 
        Claimed = 1, 
        Nurturing = 2, 
        Qualified = 3, 
        Disqualified = 4, 
        Converted = 5
    }

}
