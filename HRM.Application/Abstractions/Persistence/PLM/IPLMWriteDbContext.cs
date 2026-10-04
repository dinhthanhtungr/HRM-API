namespace HRM.Application.Abstractions.Persistence.PLM
{
    public interface IPLMWriteDbContext : IPLMDbContext
    {
        Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
