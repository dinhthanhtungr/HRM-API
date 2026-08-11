using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.HrSchema.Hrm_models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace HRM.Application.Features.Employees.Commands.CreateEmployee
{
    internal sealed class CreateEmployeeCommandHandler
        : IRequestHandler<CreateEmployeeCommand, OperationResult<Guid>>
    {
        private const int ExternalIdMaxLength = 100;
        private const int FullNameMaxLength = 200;
        private const int EmailMaxLength = 256;
        private const int PhoneNumberMaxLength = 50;
        private const int IdentifierMaxLength = 100;

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
            var currentCompanyId = _currentUser.CompanyId;
            var currentEmployeeId = _currentUser.EmployeeId;
            if (!_currentUser.IsAuthenticated || currentCompanyId is null || currentEmployeeId is null)
            {
                return OperationResult<Guid>.Fail(
                    "Tài khoản hiện tại chưa được liên kết đầy đủ với công ty và nhân viên.");
            }

            if (!_currentUser.IsInAnyRole(
                    ApplicationRoleSets.EmployeeAdministration.EmployeeManagers))
            {
                return OperationResult<Guid>.Fail("Bạn không có quyền tạo nhân viên.");
            }

            if (string.IsNullOrWhiteSpace(request.ExternalId))
            {
                return OperationResult<Guid>.Fail("Mã nhân viên là bắt buộc.");
            }

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return OperationResult<Guid>.Fail("Tên nhân viên là bắt buộc.");
            }

            if (request.ExternalId.Trim().Length > ExternalIdMaxLength)
            {
                return OperationResult<Guid>.Fail(
                    $"Mã nhân viên không được vượt quá {ExternalIdMaxLength} ký tự.");
            }

            if (request.FullName.Trim().Length > FullNameMaxLength)
            {
                return OperationResult<Guid>.Fail(
                    $"Tên nhân viên không được vượt quá {FullNameMaxLength} ký tự.");
            }

            var normalizedEmail = TrimToNull(request.Email);
            if (normalizedEmail?.Length > EmailMaxLength ||
                normalizedEmail is not null &&
                !MailAddress.TryCreate(normalizedEmail, out _))
            {
                return OperationResult<Guid>.Fail("Email không hợp lệ.");
            }

            if (request.PhoneNumber?.Trim().Length > PhoneNumberMaxLength)
            {
                return OperationResult<Guid>.Fail(
                    $"Số điện thoại không được vượt quá {PhoneNumberMaxLength} ký tự.");
            }

            if (request.Identifier?.Trim().Length > IdentifierMaxLength)
            {
                return OperationResult<Guid>.Fail(
                    $"Số định danh không được vượt quá {IdentifierMaxLength} ký tự.");
            }

            if (request.CompanyId == Guid.Empty)
            {
                return OperationResult<Guid>.Fail("Công ty là bắt buộc.");
            }

            if (request.PartId == Guid.Empty)
            {
                return OperationResult<Guid>.Fail("Bộ phận là bắt buộc.");
            }

            if (request.CompanyId != currentCompanyId &&
                !_currentUser.IsInAnyRole(
                    ApplicationRoleSets.EmployeeAdministration.GlobalCompanyManagers))
            {
                return OperationResult<Guid>.Fail(
                    "Bạn không có quyền tạo nhân viên cho công ty này.");
            }

            var companyExists = await _dbContext.Companies
                .AsNoTracking()
                .AnyAsync(
                    company =>
                        company.CompanyId == request.CompanyId &&
                        company.IsActive,
                    cancellationToken);
            if (!companyExists)
            {
                return OperationResult<Guid>.Fail(
                    "Công ty không tồn tại hoặc đã ngừng hoạt động.");
            }

            var partExistsInCompany = await _dbContext.Parts
                .AsNoTracking()
                .AnyAsync(
                    part =>
                        part.PartId == request.PartId &&
                        (_dbContext.Employees.Any(employee =>
                             employee.CompanyId == request.CompanyId &&
                             employee.PartId == part.PartId) ||
                         _dbContext.Groups.Any(group =>
                             group.CompanyId == request.CompanyId &&
                             group.PartId == part.PartId)),
                    cancellationToken);
            if (!partExistsInCompany)
            {
                return OperationResult<Guid>.Fail(
                    "Bộ phận không tồn tại hoặc không thuộc công ty đã chọn.");
            }

            if (request.WorkProfile?.PartId is { } workProfilePartId &&
                workProfilePartId != request.PartId)
            {
                return OperationResult<Guid>.Fail(
                    "Bộ phận trong hồ sơ công việc phải trùng với bộ phận của nhân viên.");
            }

            if (request.WorkProfile?.GroupId is { } groupId)
            {
                var groupIsValid = await _dbContext.Groups
                    .AsNoTracking()
                    .AnyAsync(
                        group =>
                            group.GroupId == groupId &&
                            group.CompanyId == request.CompanyId &&
                            group.PartId == request.PartId,
                        cancellationToken);
                if (!groupIsValid)
                {
                    return OperationResult<Guid>.Fail(
                        "Nhóm công việc không thuộc công ty và bộ phận đã chọn.");
                }
            }

            if (request.Contract is not null && request.Contract.StartDate is null)
            {
                return OperationResult<Guid>.Fail("Ngày bắt đầu hợp đồng là bắt buộc khi tạo thông tin hợp đồng.");
            }

            if (request.Contract?.EndDate is { } contractEndDate &&
                request.Contract.StartDate is { } contractStartDate &&
                contractEndDate < contractStartDate)
            {
                return OperationResult<Guid>.Fail(
                    "Ngày kết thúc hợp đồng không được trước ngày bắt đầu.");
            }

            if (request.WorkProfile?.EffectiveTo is { } effectiveTo &&
                request.WorkProfile.EffectiveFrom is { } effectiveFrom &&
                effectiveTo < effectiveFrom)
            {
                return OperationResult<Guid>.Fail(
                    "Ngày kết thúc hồ sơ công việc không được trước ngày hiệu lực.");
            }

            if (request.BankAccount is not null && string.IsNullOrWhiteSpace(request.BankAccount.AccountNumber))
            {
                return OperationResult<Guid>.Fail("Số tài khoản ngân hàng là bắt buộc khi tạo thông tin ngân hàng.");
            }

            var normalizedExternalId = request.ExternalId.Trim();
            var exists = await _dbContext.Employees
                .AnyAsync(
                    employee =>
                        employee.CompanyId == request.CompanyId &&
                        employee.ExternalId == normalizedExternalId,
                    cancellationToken);

            if (exists)
            {
                return OperationResult<Guid>.Fail("Mã nhân viên đã tồn tại trong công ty.");
            }

            var employeeId = Guid.CreateVersion7();
            var employee = new Employee
            {
                EmployeeId = employeeId,
                ExternalId = normalizedExternalId,
                FullName = request.FullName.Trim(),
                Gender = TrimToNull(request.Gender),
                DateOfBirth = request.DateOfBirth,
                Identifier = TrimToNull(request.Identifier),
                PhoneNumber = TrimToNull(request.PhoneNumber),
                Email = normalizedEmail,
                Address = TrimToNull(request.Address),
                PartId = request.PartId,
                CompanyId = request.CompanyId,
                DateHired = request.DateHired,
                Status = TrimToNull(request.Status),
                EndDate = request.EndDate,
                IsActive = true,
                CreatedBy = currentEmployeeId,
                CreatedDate = DateTime.UtcNow
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
                CreatedDate = DateTime.Now,
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
