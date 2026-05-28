using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Employees
{
    public enum EducationLevel
    {
        None = 0,              // Chưa xác định / không có thông tin
        Primary = 1,           // Tiểu học
        Secondary = 2,         // THCS
        HighSchool = 3,        // THPT
        Vocational = 4,        // Trung cấp nghề
        College = 5,           // Cao đẳng
        Bachelor = 6,          // Đại học / Cử nhân
        Master = 7,            // Thạc sĩ
        Doctorate = 8,         // Tiến sĩ
        Other = 99             // Khác
    }

}
