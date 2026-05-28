using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Abstractions.Persistence.Employees
{
    public interface IEmployeeReadDbContext
    {
        DbSet<Employee> Employees { get; }
        DbSet<EmployeeProfile> EmployeeProfiles { get; }
        DbSet<EmployeeWorkProfile> EmployeeWorkProfiles { get; }
        DbSet<EmployeeContract> EmployeeContracts { get; }
        DbSet<EmployeeBankAccount> EmployeeBankAccounts { get; }
        DbSet<EmployeeInsuranceProfile> EmployeeInsuranceProfiles { get; }
        DbSet<EmployeeRelative> EmployeeRelatives { get; }
        DbSet<EmployeeDocument> EmployeeDocuments { get; }
        DbSet<Part> Parts { get; }
        DbSet<Group> Groups { get; }
        DbSet<MemberInGroup> MemberInGroups { get; }
    }
}
