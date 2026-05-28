using HRM.Domain.Enums.Employees;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeProfile
    {
        public Guid EmployeeProfileId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public string? Ethnicity { get; set; }
        public EducationLevel? EducationLevel { get; set; }
        public DateOnly? IdentifierIssueDate { get; set; }
        public string? IdentifierIssuePlace { get; set; }
        public string? PermanentAddress { get; set; }
        public string? TemporaryAddress { get; set; }

        public virtual Employee Employee { get; set; } = default!;
    }
}
