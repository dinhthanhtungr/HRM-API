using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Employees
{
    public enum EmployeeDocumentStatus
    {
        Missing = 1,    // Chưa có hồ sơ
        Submitted = 2,  // Đã nộp
        Verified = 3,   // Đã kiểm tra/xác minh
        Rejected = 4    // Bị từ chối/cần bổ sung
    }
}
