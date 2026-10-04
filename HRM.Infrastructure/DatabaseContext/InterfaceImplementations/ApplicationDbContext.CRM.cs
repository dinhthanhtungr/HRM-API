using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext :
        ICRMReadDbContext,
        ICRMWriteDbContext
    {
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginQuotationTransactionAsync(
            CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        public void ClearTrackedChanges()
        {
            ChangeTracker.Clear();
        }
    }
}
