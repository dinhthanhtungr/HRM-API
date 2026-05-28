using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Identity
{
    public class ApplicationUserRole : IdentityUserRole<Guid>
    {
        public bool IsActive { get; set; } = true;
        public virtual ApplicationUser User { get; set; }
        public virtual ApplicationRole Role { get; set; }
    }
}
