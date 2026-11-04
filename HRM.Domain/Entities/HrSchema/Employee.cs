using HRM.Domain.Entities.CompanySchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public Guid EmployeeId { get; set; }

    public string ExternalId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Identifier { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public Guid? PartId { get; set; }

    public Guid? LevelId { get; set; }

    public Guid? CompanyId { get; set; }

    public DateTime? DateHired { get; set; }

    public string? Status { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? CreatedDate { get; set; } = DateTime.Now;

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Company Company { get; set; } = default!;

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual Employee? UpdatedByNavigation { get; set; }

    public virtual ICollection<Employee> CreatedEmployees { get; set; } = new List<Employee>();

    public virtual ICollection<Employee> UpdatedEmployees { get; set; } = new List<Employee>();

    public virtual Part? Part { get; set; }
}
