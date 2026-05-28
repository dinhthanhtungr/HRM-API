using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Employees
{
    public enum EmployeeDocumentType
    {
        CitizenId = 1,          // CCCD / CMND / hộ chiếu
        Contract = 2,           // Hợp đồng lao động
        Degree = 3,             // Bằng cấp / chứng chỉ
        Resume = 4,             // Sơ yếu lý lịch / CV
        HealthCertificate = 5,  // Giấy khám sức khỏe
        Other = 99              // Khác
    }

}
