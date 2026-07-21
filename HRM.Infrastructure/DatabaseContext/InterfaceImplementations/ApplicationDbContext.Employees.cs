using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Domain.Entities.HrSchema.Hrm_models;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext :
        IEmployeeReadDbContext,
        IEmployeeManagementDbContext
    {
    }

}
