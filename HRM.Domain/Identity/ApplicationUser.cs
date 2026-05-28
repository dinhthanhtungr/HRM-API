using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? personName {  get; set; }
        public string? RefreshToken {  get; set; }
        // FK → Employees
        public Guid? EmployeeId { get; set; }          // để nullable cho dễ migrate lần đầu
        public DateTime RefreshTokenExpirationDateTime {  get; set; }
        //public virtual ICollection<ApplicationUserRole> UserRoles { get; set; }
        public ICollection<ApplicationUserRole> UserRoles { get; set; }

        public Employee? Employee { get; set; }         // navigation
    }
}
