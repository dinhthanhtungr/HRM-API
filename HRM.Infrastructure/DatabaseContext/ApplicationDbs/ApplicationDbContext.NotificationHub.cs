using HRM.Application.Abstractions.Persistence.NotificationHub;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : INotificationHubWriteDbContext
{
    public Task<IDbContextTransaction> BeginNotificationHubTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        // All three updates must see the same committed set, including when a sender commits mid-request.
        return Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
    }
}
