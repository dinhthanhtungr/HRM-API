using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.WareHouses
{
    public enum VoucherDetailType
    {
        Import = 1,
        Export = 2,
        Transfer = 3,
        Adjustment = 4,
        Return = 5,
        Waiter = 7,
        QCPass = 8,
        QCFail = 9,
        Special = 10,
    }
}
