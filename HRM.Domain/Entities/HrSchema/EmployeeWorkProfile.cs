using HRM.Domain.Entities.CompanySchema;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeWorkProfile
    {
        public Guid EmployeeWorkProfileId { get; set; } = Guid.CreateVersion7();
        public Guid EmployeeId { get; set; }

        public string? AttendanceCode { get; set; }
        public Guid? PartId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? JobTitleId { get; set; }
        public string? WorkLocation { get; set; }
        public DateOnly? ProbationEndDate { get; set; }

        public bool IsCurrent { get; set; } = true;
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public bool IsActive { get; set; } = true;

        public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public DateOnly? OnboardingTrainingDate { get; set; }

        public virtual Employee Employee { get; set; } = default!;
        public virtual Part? Part { get; set; }
        public virtual Group? Group { get; set; }
        public virtual JobTitle? JobTitle { get; set; }

        public virtual Employee CreatedByNavigation { get; set; } = default!;
    }
}
