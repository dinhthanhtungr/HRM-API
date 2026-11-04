using HRM.Domain.Entities.HrSchema.Hrm_models;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public virtual EmployeeProfile? EmployeeProfile { get; set; }
    public virtual ICollection<EmployeeWorkProfile> EmployeeWorkProfiles { get; set; } = new List<EmployeeWorkProfile>();
    public virtual ICollection<EmployeeContract> EmployeeContracts { get; set; } = new List<EmployeeContract>();
    public virtual ICollection<EmployeeBankAccount> EmployeeBankAccounts { get; set; } = new List<EmployeeBankAccount>();
    public virtual ICollection<EmployeeInsuranceProfile> EmployeeInsuranceProfiles { get; set; } = new List<EmployeeInsuranceProfile>();
    public virtual ICollection<EmployeeRelative> EmployeeRelatives { get; set; } = new List<EmployeeRelative>();
    public virtual ICollection<EmployeeDocument> EmployeeDocuments { get; set; } = new List<EmployeeDocument>();
    public virtual ICollection<EmployeeWorkProfile> EmployeeWorkProfileCreatedByNavigations { get; set; } = new List<EmployeeWorkProfile>();
    public virtual ICollection<PayrollEmployeeRun> PayrollEmployeeRuns { get; set; } = new List<PayrollEmployeeRun>();
    public virtual ICollection<EmployeeInsuranceContribution> EmployeeInsuranceContributions { get; set; } = new List<EmployeeInsuranceContribution>();
    public virtual ICollection<EmployeeInsuranceBook> EmployeeInsuranceBooks { get; set; } = new List<EmployeeInsuranceBook>();
    public virtual ICollection<EmployeeInsuranceClaim> EmployeeInsuranceClaims { get; set; } = new List<EmployeeInsuranceClaim>();
}
