using HRM.Domain.Entities.CompanySchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<Company> CompanyCreatedByNavigations { get; set; } = new List<Company>();
    public virtual ICollection<Company> CompanyUpdatedByNavigations { get; set; } = new List<Company>();
    public virtual ICollection<Group> GroupCreatedByNavigations { get; set; } = new List<Group>();
    public virtual ICollection<Group> GroupUpdatedByNavigations { get; set; } = new List<Group>();
    public virtual ICollection<MemberInGroup> MemberInGroups { get; set; } = new List<MemberInGroup>();
}
