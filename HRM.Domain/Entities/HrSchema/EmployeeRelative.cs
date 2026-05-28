using HRM.Domain.Enums.Employees;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeRelative
    {
        public Guid EmployeeRelativeId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public string FullName { get; set; } = default!;
        public EmployeeRelationshipType? Relationship { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsEmergencyContact { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }
}
