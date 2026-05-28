using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Identity
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        //public bool IsActive { get; set; } = true;
        //public virtual ICollection<ApplicationUserRole> UserRoles { get; set; }
        public ICollection<ApplicationUserRole> UserRoles { get; set; }
    }
}
