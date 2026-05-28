using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Devandqa
{
    public enum QcDecision 
    {
        Special = 10,  // CHấp nhận đặc biệt
        QCPass = 8, // Chấp nhận
        Waiter = 7, // Tạm giữ
        QCFail = 9 // Từ chối
    }
}
