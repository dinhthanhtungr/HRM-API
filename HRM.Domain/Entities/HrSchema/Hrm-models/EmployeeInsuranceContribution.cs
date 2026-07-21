using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.HrSchema
{
    public class EmployeeInsuranceContribution
    {
        public Guid EmployeeInsuranceContributionId { get; set; } = Guid.CreateVersion7();

        public Guid EmployeeId { get; set; }
        public Guid? PayrollPeriodId { get; set; }

        public DateOnly ContributionMonth { get; set; }

        public string? EmployeeNameSnapshot { get; set; }
        public string? DepartmentSnapshot { get; set; }
        public string? JobTitleSnapshot { get; set; }
        public string? SocialInsuranceNumberSnapshot { get; set; }

        public decimal? SocialInsuranceSalary { get; set; }
        public decimal? UnemploymentInsuranceSalary { get; set; }

        public decimal? CompanyRetirementRate { get; set; }
        public decimal? CompanySicknessMaternityRate { get; set; }
        public decimal? CompanyOccupationalAccidentRate { get; set; }
        public decimal? CompanyHealthInsuranceRate { get; set; }
        public decimal? CompanyUnemploymentRate { get; set; }
        public decimal? CompanyTotalRate { get; set; }

        public decimal? EmployeeSocialInsuranceRate { get; set; }
        public decimal? EmployeeHealthInsuranceRate { get; set; }
        public decimal? EmployeeUnemploymentRate { get; set; }
        public decimal? EmployeeTotalRate { get; set; }
        public decimal? MonthlyTotalPayableRate { get; set; }

        public decimal? CompanyRetirementAmount { get; set; }
        public decimal? CompanySicknessMaternityAmount { get; set; }
        public decimal? CompanyOccupationalAccidentAmount { get; set; }
        public decimal? CompanyHealthInsuranceAmount { get; set; }
        public decimal? CompanyUnemploymentAmount { get; set; }
        public decimal? CompanyTotalAmount { get; set; }

        public decimal? EmployeeSocialInsuranceAmount { get; set; }
        public decimal? EmployeeHealthInsuranceAmount { get; set; }
        public decimal? EmployeeUnemploymentAmount { get; set; }
        public decimal? EmployeeTotalAmount { get; set; }
        public decimal? MonthlyTotalPayableAmount { get; set; }

        public string? Note { get; set; }

        public virtual Employee Employee { get; set; } = default!;
        public virtual PayrollPeriod? PayrollPeriod { get; set; }
    }
}
