using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.HrSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Employees.Commands.CreateEmployee
{
    internal sealed class CreateEmployeeCommandHandler
        : IRequestHandler<CreateEmployeeCommand, OperationResult<Guid>>
    {
        private readonly IEmployeeManagementDbContext _dbContext;
        private readonly ICurrentUser _currentUser;

        public CreateEmployeeCommandHandler(
            IEmployeeManagementDbContext dbContext,
            ICurrentUser currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        public async Task<OperationResult<Guid>> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ExternalId))
            {
                return OperationResult<Guid>.Fail("Yêu cầu phải có mã nhân viên.");
            }

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return OperationResult<Guid>.Fail("Yêu cầu phải có tên nhân viên.");
            }

            if (request.Contract is not null && request.Contract.StartDate is null)
            {
                return OperationResult<Guid>.Fail("Ngày bắt đầu hợp đồng là bắt buộc khi tạo thông tin hợp đồng.");
            }

            if (request.BankAccount is not null && string.IsNullOrWhiteSpace(request.BankAccount.AccountNumber))
            {
                return OperationResult<Guid>.Fail("Số tài khoản ngân hàng là bắt buộc khi tạo thông tin ngân hàng.");
            }

            var exists = await _dbContext.Employees
                .AnyAsync(x => x.ExternalId == request.ExternalId, cancellationToken);

            if (exists)
            {
                return OperationResult<Guid>.Fail("Mã nhân viên đã tồn tại.");
            }

            var employeeId = Guid.CreateVersion7();
            var employee = new Employee
            {
                EmployeeId = employeeId,
                ExternalId = request.ExternalId.Trim(),
                FullName = request.FullName.Trim(),
                Gender = TrimToNull(request.Gender),
                DateOfBirth = request.DateOfBirth,
                Identifier = TrimToNull(request.Identifier),
                PhoneNumber = TrimToNull(request.PhoneNumber),
                Email = TrimToNull(request.Email),
                Address = TrimToNull(request.Address),
                PartId = request.PartId,
                CompanyId = request.CompanyId,
                DateHired = request.DateHired,
                Status = TrimToNull(request.Status),
                EndDate = request.EndDate,
                IsActive = true
            };

            AddProfile(employee, request.Profile);
            AddWorkProfile(employee, request.WorkProfile, request.PartId);
            AddContract(employee, request.Contract);
            AddBankAccount(employee, request.BankAccount);
            AddInsuranceProfile(employee, request.InsuranceProfile);
            AddRelatives(employee, request.Relatives);
            AddDocuments(employee, request.Documents);

            _dbContext.Employees.Add(employee);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<Guid>.Ok(employee.EmployeeId);
        }

        private static void AddProfile(Employee employee, CreateEmployeeProfileRequest? profile)
        {
            if (profile is null || !HasProfileData(profile))
            {
                return;
            }

            employee.EmployeeProfile = new EmployeeProfile
            {
                EmployeeId = employee.EmployeeId,
                Ethnicity = TrimToNull(profile.Ethnicity),
                EducationLevel = profile.EducationLevel,
                IdentifierIssueDate = profile.IdentifierIssueDate,
                IdentifierIssuePlace = TrimToNull(profile.IdentifierIssuePlace),
                PermanentAddress = TrimToNull(profile.PermanentAddress),
                TemporaryAddress = TrimToNull(profile.TemporaryAddress)
            };
        }

        private void AddWorkProfile(Employee employee, CreateEmployeeWorkProfileRequest? workProfile, Guid? fallbackPartId)
        {
            if (workProfile is null || !HasWorkProfileData(workProfile, fallbackPartId))
            {
                return;
            }

            employee.EmployeeWorkProfiles.Add(new EmployeeWorkProfile
            {
                EmployeeId = employee.EmployeeId,
                AttendanceCode = TrimToNull(workProfile.AttendanceCode),
                PartId = workProfile.PartId ?? fallbackPartId,
                GroupId = workProfile.GroupId,
                JobTitleId = workProfile.JobTitleId,
                WorkLocation = TrimToNull(workProfile.WorkLocation),
                ProbationEndDate = workProfile.ProbationEndDate,
                IsCurrent = true,
                EffectiveFrom = workProfile.EffectiveFrom ?? DateOnly.FromDateTime(employee.DateHired ?? DateTime.Today),
                EffectiveTo = workProfile.EffectiveTo,
                IsActive = true,
                CreatedBy = _currentUser.EmployeeId ?? employee.EmployeeId,
                CreatedDate = DateTime.UtcNow,
                OnboardingTrainingDate = workProfile.OnboardingTrainingDate
            });
        }

        private static void AddContract(Employee employee, CreateEmployeeContractRequest? contract)
        {
            if (contract is null)
            {
                return;
            }

            employee.EmployeeContracts.Add(new EmployeeContract
            {
                EmployeeId = employee.EmployeeId,
                ContractNo = TrimToNull(contract.ContractNo),
                ContractType = contract.ContractType,
                StartDate = contract.StartDate!.Value,
                EndDate = contract.EndDate,
                IsCurrent = contract.IsCurrent
            });
        }

        private static void AddBankAccount(Employee employee, CreateEmployeeBankAccountRequest? bankAccount)
        {
            if (bankAccount is null)
            {
                return;
            }

            employee.EmployeeBankAccounts.Add(new EmployeeBankAccount
            {
                EmployeeId = employee.EmployeeId,
                BankName = TrimToNull(bankAccount.BankName),
                AccountNumber = bankAccount.AccountNumber.Trim(),
                AccountHolder = TrimToNull(bankAccount.AccountHolder),
                IsPayrollAccount = bankAccount.IsPayrollAccount
            });
        }

        private static void AddInsuranceProfile(Employee employee, CreateEmployeeInsuranceProfileRequest? insuranceProfile)
        {
            if (insuranceProfile is null || !HasInsuranceData(insuranceProfile))
            {
                return;
            }

            employee.EmployeeInsuranceProfiles.Add(new EmployeeInsuranceProfile
            {
                EmployeeId = employee.EmployeeId,
                SocialInsuranceNumber = TrimToNull(insuranceProfile.SocialInsuranceNumber),
                TaxCode = TrimToNull(insuranceProfile.TaxCode),
                HealthInsuranceNumber = TrimToNull(insuranceProfile.HealthInsuranceNumber)
            });
        }

        private static void AddRelatives(Employee employee, IEnumerable<CreateEmployeeRelativeRequest> relatives)
        {
            foreach (var relative in relatives)
            {
                if (string.IsNullOrWhiteSpace(relative.FullName))
                {
                    continue;
                }

                employee.EmployeeRelatives.Add(new EmployeeRelative
                {
                    EmployeeId = employee.EmployeeId,
                    FullName = relative.FullName.Trim(),
                    Relationship = relative.Relationship,
                    PhoneNumber = TrimToNull(relative.PhoneNumber),
                    IsEmergencyContact = relative.IsEmergencyContact
                });
            }
        }

        private static void AddDocuments(Employee employee, IEnumerable<CreateEmployeeDocumentRequest> documents)
        {
            foreach (var document in documents)
            {
                employee.EmployeeDocuments.Add(new EmployeeDocument
                {
                    EmployeeId = employee.EmployeeId,
                    DocumentType = document.DocumentType,
                    DocumentName = TrimToNull(document.DocumentName),
                    Status = document.Status,
                    Note = TrimToNull(document.Note)
                });
            }
        }

        private static bool HasProfileData(CreateEmployeeProfileRequest profile)
        {
            return !string.IsNullOrWhiteSpace(profile.Ethnicity)
                || profile.EducationLevel.HasValue
                || profile.IdentifierIssueDate.HasValue
                || !string.IsNullOrWhiteSpace(profile.IdentifierIssuePlace)
                || !string.IsNullOrWhiteSpace(profile.PermanentAddress)
                || !string.IsNullOrWhiteSpace(profile.TemporaryAddress);
        }

        private static bool HasWorkProfileData(CreateEmployeeWorkProfileRequest workProfile, Guid? fallbackPartId)
        {
            return !string.IsNullOrWhiteSpace(workProfile.AttendanceCode)
                || (workProfile.PartId ?? fallbackPartId).HasValue
                || workProfile.GroupId.HasValue
                || workProfile.JobTitleId.HasValue
                || !string.IsNullOrWhiteSpace(workProfile.WorkLocation)
                || workProfile.ProbationEndDate.HasValue
                || workProfile.EffectiveFrom.HasValue
                || workProfile.EffectiveTo.HasValue
                || workProfile.OnboardingTrainingDate.HasValue;
        }

        private static bool HasInsuranceData(CreateEmployeeInsuranceProfileRequest insuranceProfile)
        {
            return !string.IsNullOrWhiteSpace(insuranceProfile.SocialInsuranceNumber)
                || !string.IsNullOrWhiteSpace(insuranceProfile.TaxCode)
                || !string.IsNullOrWhiteSpace(insuranceProfile.HealthInsuranceNumber);
        }

        private static string? TrimToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
