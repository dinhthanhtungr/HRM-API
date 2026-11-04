using HRM.Domain.Entities.AttachmentSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual ICollection<AttachmentModel> AttachmentCreatedByNavigations { get; set; } = new List<AttachmentModel>(); // Fix for IDE0028
}
