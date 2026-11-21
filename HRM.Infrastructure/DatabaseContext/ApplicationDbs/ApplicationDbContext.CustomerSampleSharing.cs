using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Entities.CustomerSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : ICustomerSampleSharingDbContext
{
    public DbSet<CustomerSampleSharingGroup> CustomerSampleSharingGroups { get; set; } = null!;
    public DbSet<CustomerSampleSharingMember> CustomerSampleSharingMembers { get; set; } = null!;
    public Task<IDbContextTransaction> BeginCustomerSampleSharingTransactionAsync(CancellationToken ct)
        => Database.BeginTransactionAsync(ct);
    public Task LockCustomerSampleSharingAsync(Guid companyId, CancellationToken ct)
        => Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"customer-sample-sharing:" + companyId.ToString("D")}, 0))", ct);
}
