using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext :
        ICRMReadDbContext,
        ICRMWriteDbContext
    {
        public void ClearTrackedChanges()
        {
            ChangeTracker.Clear();
        }
    }
}
