using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Queries.GetEmployeeById
{
    internal sealed class GetEmployeeByIdQueryHandler
        : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto?>
    {
        private readonly IEmployeeReadDbContext _dbContext;

        public GetEmployeeByIdQueryHandler(IEmployeeReadDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<EmployeeDetailDto?> Handle(
            GetEmployeeByIdQuery request,
            CancellationToken cancellationToken)
        {
            return await _dbContext.Employees
                .Where(x => x.EmployeeId == request.EmployeeId)
                .Select(x => new EmployeeDetailDto
                {
                    EmployeeId = x.EmployeeId,
                    ExternalId = x.ExternalId,
                    FullName = x.FullName,
                    Gender = x.Gender,
                    DateOfBirth = x.DateOfBirth,
                    Identifier = x.Identifier,
                    PhoneNumber = x.PhoneNumber,
                    Email = x.Email,
                    Address = x.Address,
                    PartId = x.PartId,
                    CompanyId = x.CompanyId,
                    DateHired = x.DateHired,
                    Status = x.Status,
                    EndDate = x.EndDate,
                    IsActive = x.IsActive,
                    Profile = x.EmployeeProfile == null
                        ? null
                        : new EmployeeProfileDto
                        {
                            EmployeeProfileId = x.EmployeeProfile.EmployeeProfileId,
                            Ethnicity = x.EmployeeProfile.Ethnicity,
                            EducationLevel = x.EmployeeProfile.EducationLevel,
                            IdentifierIssueDate = x.EmployeeProfile.IdentifierIssueDate,
                            IdentifierIssuePlace = x.EmployeeProfile.IdentifierIssuePlace,
                            PermanentAddress = x.EmployeeProfile.PermanentAddress,
                            TemporaryAddress = x.EmployeeProfile.TemporaryAddress
                        },
                    WorkProfiles = x.EmployeeWorkProfiles
                        .Select(p => new EmployeeWorkProfileDto
                        {
                            EmployeeWorkProfileId = p.EmployeeWorkProfileId,
                            AttendanceCode = p.AttendanceCode,
                            PartId = p.PartId,
                            GroupId = p.GroupId,
                            JobTitleId = p.JobTitleId,
                            WorkLocation = p.WorkLocation,
                            ProbationEndDate = p.ProbationEndDate,
                            IsCurrent = p.IsCurrent,
                            EffectiveFrom = p.EffectiveFrom,
                            EffectiveTo = p.EffectiveTo,
                            IsActive = p.IsActive,
                            OnboardingTrainingDate = p.OnboardingTrainingDate
                        })
                        .ToArray(),
                    Contracts = x.EmployeeContracts
                        .Select(c => new EmployeeContractDto
                        {
                            EmployeeContractId = c.EmployeeContractId,
                            ContractNo = c.ContractNo,
                            ContractType = c.ContractType,
                            StartDate = c.StartDate,
                            EndDate = c.EndDate,
                            IsCurrent = c.IsCurrent
                        })
                        .ToArray(),
                    BankAccounts = x.EmployeeBankAccounts
                        .Select(b => new EmployeeBankAccountDto
                        {
                            EmployeeBankAccountId = b.EmployeeBankAccountId,
                            BankName = b.BankName,
                            AccountNumber = b.AccountNumber,
                            AccountHolder = b.AccountHolder,
                            IsPayrollAccount = b.IsPayrollAccount
                        })
                        .ToArray(),
                    InsuranceProfiles = x.EmployeeInsuranceProfiles
                        .Select(i => new EmployeeInsuranceProfileDto
                        {
                            EmployeeInsuranceProfileId = i.EmployeeInsuranceProfileId,
                            SocialInsuranceNumber = i.SocialInsuranceNumber,
                            TaxCode = i.TaxCode,
                            HealthInsuranceNumber = i.HealthInsuranceNumber
                        })
                        .ToArray(),
                    Relatives = x.EmployeeRelatives
                        .Select(r => new EmployeeRelativeDto
                        {
                            EmployeeRelativeId = r.EmployeeRelativeId,
                            FullName = r.FullName,
                            Relationship = r.Relationship,
                            PhoneNumber = r.PhoneNumber,
                            IsEmergencyContact = r.IsEmergencyContact
                        })
                        .ToArray(),
                    Documents = x.EmployeeDocuments
                        .Select(d => new EmployeeDocumentDto
                        {
                            EmployeeDocumentId = d.EmployeeDocumentId,
                            DocumentType = d.DocumentType,
                            DocumentName = d.DocumentName,
                            Status = d.Status,
                            Note = d.Note
                        })
                        .ToArray()
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
