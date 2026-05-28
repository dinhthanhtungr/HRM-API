namespace HRM.Domain.Entities.HrSchema
{
    public class JobTitle
    {
        public Guid JobTitleId { get; set; } = Guid.CreateVersion7();

        public string Name { get; set; } = default!;

        public string? EnglishName { get; set; }

        public string? Code { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<EmployeeWorkProfile> EmployeeWorkProfiles { get; set; } = new List<EmployeeWorkProfile>();
    }
}
