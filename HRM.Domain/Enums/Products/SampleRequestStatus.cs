using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Products
{
    public enum SampleRequestStatus
    {
        New,            // Mới
        SampleReceived, // Nhận mẫu
        InProgress,     // Đang xử lý
        SampleSent,     // Gửi mẫu
        Cancelled,      // Hủy
        Completed        // Hoàn thành
    }
}
