using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.CRM.CustomerCare;

public interface ICustomerSampleSharingDbContext
{
    DbSet<CustomerSampleSharingGroup> CustomerSampleSharingGroups { get; }
    DbSet<CustomerSampleSharingMember> CustomerSampleSharingMembers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<SampleRequest> SampleRequests { get; }
    DbSet<AuditLog> AuditLogs { get; }
    Task<IDbContextTransaction> BeginCustomerSampleSharingTransactionAsync(CancellationToken ct);
    Task LockCustomerSampleSharingAsync(Guid companyId, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
