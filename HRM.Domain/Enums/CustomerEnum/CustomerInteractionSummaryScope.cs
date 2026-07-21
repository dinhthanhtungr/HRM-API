using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.CustomerEnum
{
    public enum CustomerInteractionSummaryScope
    {
        Monthly = 1,      // Tóm tắt theo tháng
        Yearly = 2,       // Tóm tắt theo năm
        Lifetime = 3,     // Từ lúc bắt đầu đến hiện tại
        CustomRange = 4   // Khoảng thời gian tùy chọn
    }
}
