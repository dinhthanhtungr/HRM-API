using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Employees
{
    public enum EmployeeContractType
    {
        Probation = 1,        // Hợp đồng thử việc
        FixedTerm = 2,        // Hợp đồng xác định thời hạn
        IndefiniteTerm = 3,   // Hợp đồng không xác định thời hạn
        Seasonal = 4,         // Hợp đồng thời vụ
        Service = 5,          // Hợp đồng dịch vụ/cộng tác viên
        Other = 99            // Khác
    }
}
