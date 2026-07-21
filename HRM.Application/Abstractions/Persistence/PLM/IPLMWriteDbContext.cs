namespace HRM.Application.Abstractions.Persistence.PLM
{
    public interface IPLMWriteDbContext : IPLMDbContext
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
