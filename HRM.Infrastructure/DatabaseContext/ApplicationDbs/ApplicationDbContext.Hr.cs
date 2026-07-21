using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.HrSchema.Hrm_models;
using HRM.Domain.Entities.HrSchema.Salary_models;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<EmployeeProfile> EmployeeProfiles { get; set; } = default!;
        public virtual DbSet<EmployeeWorkProfile> EmployeeWorkProfiles { get; set; } = default!;
        public virtual DbSet<EmployeeContract> EmployeeContracts { get; set; } = default!;
        public virtual DbSet<EmployeeBankAccount> EmployeeBankAccounts { get; set; } = default!;
        public virtual DbSet<EmployeeInsuranceProfile> EmployeeInsuranceProfiles { get; set; } = default!;
        public virtual DbSet<EmployeeRelative> EmployeeRelatives { get; set; } = default!;
        public virtual DbSet<EmployeeDocument> EmployeeDocuments { get; set; } = default!;
        public virtual DbSet<JobTitle> JobTitles { get; set; } = default!;


        public virtual DbSet<Employee> Employees { get; set; } = default!;
        public virtual DbSet<Part> Parts { get; set; } = default!;
        public virtual DbSet<PayrollPeriod> PayrollPeriods { get; set; } = default!;
        public virtual DbSet<PayrollEmployeeRun> PayrollEmployeeRuns { get; set; } = default!;
        public virtual DbSet<PayrollEmployeeRunDetail> PayrollEmployeeRunDetails { get; set; } = default!;
        public virtual DbSet<SalaryComponentDefinition> SalaryComponentDefinitions { get; set; } = default!;
        public virtual DbSet<EmployeeSalaryPackage> EmployeeSalaryPackages { get; set; } = default!;
        public virtual DbSet<EmployeeSalaryPackageComponent> EmployeeSalaryPackageComponents { get; set; } = default!;
    }
}
