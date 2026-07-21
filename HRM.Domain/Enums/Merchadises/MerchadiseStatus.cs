using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Merchadises
{
    public enum MerchadiseStatus
    {
        New = 0,          // Mới
        Approved = 1,     // Duyệt
        Pending = 2,      // Đang chờ
        Processing = 3,   // Đang xử lý
        Delivering = 4,   // Đang giao hàng
        Delivered = 5,    // Đã giao hàng
        Paused = 6,       // Tạm dừng
        Cancelled = 7,    // Hủy
        Completed = 8     // Hoàn thành

    }
}
