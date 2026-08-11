using HRM.Application.Commons.Models;
using HRM.Domain.Enums.Employees;
using MediatR;

namespace HRM.Application.Features.Employees.Commands.CreateEmployee
{
    public sealed class CreateEmployeeCommand : IRequest<OperationResult<Guid>>
    {
        public string ExternalId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string? Gender { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public string? Identifier { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Email { get; init; }
        public string? Address { get; init; }
        public Guid PartId { get; init; }
        public Guid CompanyId { get; init; }
        public DateTime? DateHired { get; init; }
        public string? Status { get; init; }
        public DateOnly? EndDate { get; init; }

        public CreateEmployeeProfileRequest? Profile { get; init; }
        public CreateEmployeeWorkProfileRequest? WorkProfile { get; init; }
        public CreateEmployeeContractRequest? Contract { get; init; }
        public CreateEmployeeBankAccountRequest? BankAccount { get; init; }
        public CreateEmployeeInsuranceProfileRequest? InsuranceProfile { get; init; }
        public IReadOnlyCollection<CreateEmployeeRelativeRequest> Relatives { get; init; } = [];
        public IReadOnlyCollection<CreateEmployeeDocumentRequest> Documents { get; init; } = [];
    }

 
}
