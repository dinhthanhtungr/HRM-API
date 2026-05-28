using HRM.Application.Abstractions.Persistence.Employees;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext :
        IEmployeeReadDbContext,
        IEmployeeManagementDbContext
    {
    }

}
