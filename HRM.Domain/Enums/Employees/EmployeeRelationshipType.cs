using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Employees
{
    public enum EmployeeRelationshipType
    {
        Father = 1,    // Cha
        Mother = 2,    // Mẹ
        Spouse = 3,    // Vợ/chồng
        Child = 4,     // Con
        Sibling = 5,   // Anh/chị/em ruột
        Relative = 6,  // Người thân khác
        Other = 99     // Khác
    }
}
