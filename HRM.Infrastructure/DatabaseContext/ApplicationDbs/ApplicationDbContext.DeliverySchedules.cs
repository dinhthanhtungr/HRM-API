using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IDeliveryScheduleDbContext
{
    public DbSet<DeliveryOrderScheduleAllocation> DeliveryOrderScheduleAllocations { get; set; } = null!;
    public Task<IDbContextTransaction> BeginDeliveryScheduleTransactionAsync(CancellationToken ct) => Database.BeginTransactionAsync(ct);
    public Task LockDeliveryScheduleAsync(Guid companyId, Guid detailId, CancellationToken ct)
        => Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"delivery-schedule:" + companyId.ToString("D") + ":" + detailId.ToString("D")}, 0))", ct);
}
