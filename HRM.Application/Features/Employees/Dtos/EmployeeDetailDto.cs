using HRM.Domain.Enums.Employees;

namespace HRM.Application.Features.Employees.Dtos
{
    public sealed class EmployeeDetailDto
    {
        public Guid EmployeeId { get; init; }
        public string ExternalId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string? Gender { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public string? Identifier { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Email { get; init; }
        public string? Address { get; init; }
        public Guid? PartId { get; init; }
        public Guid? CompanyId { get; init; }
        public DateTime? DateHired { get; init; }
        public string? Status { get; init; }
        public DateOnly? EndDate { get; init; }
        public bool IsActive { get; init; }

        public EmployeeProfileDto? Profile { get; init; }
        public IReadOnlyCollection<EmployeeWorkProfileDto> WorkProfiles { get; init; } = [];
        public IReadOnlyCollection<EmployeeContractDto> Contracts { get; init; } = [];
        public IReadOnlyCollection<EmployeeBankAccountDto> BankAccounts { get; init; } = [];
        public IReadOnlyCollection<EmployeeInsuranceProfileDto> InsuranceProfiles { get; init; } = [];
        public IReadOnlyCollection<EmployeeRelativeDto> Relatives { get; init; } = [];
        public IReadOnlyCollection<EmployeeDocumentDto> Documents { get; init; } = [];
    }

  
}
