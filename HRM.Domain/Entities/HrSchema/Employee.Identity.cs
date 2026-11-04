using HRM.Domain.Identity;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<ApplicationUser> ApplicationUsers { get; set; } = new List<ApplicationUser>();
}
