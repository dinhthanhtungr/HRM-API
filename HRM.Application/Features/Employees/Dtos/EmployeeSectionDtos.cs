using HRM.Domain.Enums.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Dtos
{
    public sealed class EmployeeProfileDto
    {
        public Guid EmployeeProfileId { get; init; }
        public string? Ethnicity { get; init; }
        public EducationLevel? EducationLevel { get; init; }
        public DateOnly? IdentifierIssueDate { get; init; }
        public string? IdentifierIssuePlace { get; init; }
        public string? PermanentAddress { get; init; }
        public string? TemporaryAddress { get; init; }
    }

    public sealed class EmployeeWorkProfileDto
    {
        public Guid EmployeeWorkProfileId { get; init; }
        public string? AttendanceCode { get; init; }
        public Guid? PartId { get; init; }
        public Guid? GroupId { get; init; }
        public Guid? JobTitleId { get; init; }
        public string? WorkLocation { get; init; }
        public DateOnly? ProbationEndDate { get; init; }
        public bool IsCurrent { get; init; }
        public DateOnly EffectiveFrom { get; init; }
        public DateOnly? EffectiveTo { get; init; }
        public bool IsActive { get; init; }
        public DateOnly? OnboardingTrainingDate { get; init; }
    }

    public sealed class EmployeeContractDto
    {
        public Guid EmployeeContractId { get; init; }
        public string? ContractNo { get; init; }
        public EmployeeContractType ContractType { get; init; }
        public DateOnly StartDate { get; init; }
        public DateOnly? EndDate { get; init; }
        public bool IsCurrent { get; init; }
    }

    public sealed class EmployeeBankAccountDto
    {
        public Guid EmployeeBankAccountId { get; init; }
        public string? BankName { get; init; }
        public string AccountNumber { get; init; } = string.Empty;
        public string? AccountHolder { get; init; }
        public bool IsPayrollAccount { get; init; }
    }

    public sealed class EmployeeInsuranceProfileDto
    {
        public Guid EmployeeInsuranceProfileId { get; init; }
        public string? SocialInsuranceNumber { get; init; }
        public string? TaxCode { get; init; }
        public string? HealthInsuranceNumber { get; init; }
    }

    public sealed class EmployeeRelativeDto
    {
        public Guid EmployeeRelativeId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public EmployeeRelationshipType? Relationship { get; init; }
        public string? PhoneNumber { get; init; }
        public bool IsEmergencyContact { get; init; }
    }

    public sealed class EmployeeDocumentDto
    {
        public Guid EmployeeDocumentId { get; init; }
        public EmployeeDocumentType DocumentType { get; init; }
        public string? DocumentName { get; init; }
        public EmployeeDocumentStatus Status { get; init; }
        public string? Note { get; init; }
    }
}
