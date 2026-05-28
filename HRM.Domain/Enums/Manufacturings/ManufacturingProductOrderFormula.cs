using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Manufacturings
{
    public enum ManufacturingProductOrderFormula
    {
        UnKnow = 0,
        New = 1,            // Mới
        Checking = 2,       // Đang kiểm tra
        IsSelect = 3,       // Duyệt công thức
        Processing = 4,     // Đang xử lý
        Cancelled = 5,      // Hủy
        Error = 6,          // Lỗi
        Completed = 7       // Hoàn thành
    }
}
