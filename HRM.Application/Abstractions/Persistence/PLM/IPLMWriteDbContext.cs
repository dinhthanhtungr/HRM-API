namespace HRM.Application.Abstractions.Persistence.PLM
{
    public interface IPLMWriteDbContext : IPLMDbContext
    {
        Task LockMaterialSupplierPricesAsync(Guid materialId, Guid companyId, CancellationToken cancellationToken = default);
        Task LockPurchasingCatalogAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task EnsurePurchasingCatalogCounterAsync(string prefix, bool material, CancellationToken cancellationToken = default);
        Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
