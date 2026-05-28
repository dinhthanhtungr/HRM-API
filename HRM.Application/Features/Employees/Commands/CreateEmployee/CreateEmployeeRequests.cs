using HRM.Domain.Enums.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Commands.CreateEmployee
{
    public sealed class CreateEmployeeProfileRequest
    {
        public string? Ethnicity { get; init; }
        public EducationLevel? EducationLevel { get; init; }
        public DateOnly? IdentifierIssueDate { get; init; }
        public string? IdentifierIssuePlace { get; init; }
        public string? PermanentAddress { get; init; }
        public string? TemporaryAddress { get; init; }
    }

    public sealed class CreateEmployeeWorkProfileRequest
    {
        public string? AttendanceCode { get; init; }
        public Guid? PartId { get; init; }
        public Guid? GroupId { get; init; }
        public Guid? JobTitleId { get; init; }
        public string? WorkLocation { get; init; }
        public DateOnly? ProbationEndDate { get; init; }
        public DateOnly? EffectiveFrom { get; init; }
        public DateOnly? EffectiveTo { get; init; }
        public DateOnly? OnboardingTrainingDate { get; init; }
    }

    public sealed class CreateEmployeeContractRequest
    {
        public string? ContractNo { get; init; }
        public EmployeeContractType ContractType { get; init; }
        public DateOnly? StartDate { get; init; }
        public DateOnly? EndDate { get; init; }
        public bool IsCurrent { get; init; } = true;
    }

    public sealed class CreateEmployeeBankAccountRequest
    {
        public string? BankName { get; init; }
        public string AccountNumber { get; init; } = string.Empty;
        public string? AccountHolder { get; init; }
        public bool IsPayrollAccount { get; init; } = true;
    }

    public sealed class CreateEmployeeInsuranceProfileRequest
    {
        public string? SocialInsuranceNumber { get; init; }
        public string? TaxCode { get; init; }
        public string? HealthInsuranceNumber { get; init; }
    }

    public sealed class CreateEmployeeRelativeRequest
    {
        public string FullName { get; init; } = string.Empty;
        public EmployeeRelationshipType? Relationship { get; init; }
        public string? PhoneNumber { get; init; }
        public bool IsEmergencyContact { get; init; }
    }

    public sealed class CreateEmployeeDocumentRequest
    {
        public EmployeeDocumentType DocumentType { get; init; }
        public string? DocumentName { get; init; }
        public EmployeeDocumentStatus Status { get; init; } = EmployeeDocumentStatus.Missing;
        public string? Note { get; init; }
    }
}
