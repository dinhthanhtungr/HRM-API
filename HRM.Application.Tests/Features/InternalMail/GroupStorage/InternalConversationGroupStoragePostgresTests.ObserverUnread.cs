using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task ObserverUnreadCountsUseOwnInboxWithoutRequiringOrCreatingMembership()
    {
        // Isolated synthetic cluster: no application DB connection is used by this fixture.
        await postgres.ExecuteAsync("DELETE FROM \"InternalMail\".\"InternalConversationParticipants\";");
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var company = Guid.Parse(Company);
        var employee = Guid.Parse(Employee);
        Assert.Empty(await db.QueryInternalConversationNotificationUnreadCounts(company, employee).ToArrayAsync());
        var count = Assert.Single(await db.QueryInternalConversationNotificationUnreadCounts(company, employee,
            allowCompanyObserver: true).ToArrayAsync());
        Assert.Equal(1, count.UnreadCount);
        Assert.Empty(await db.QueryInternalConversationNotificationUnreadCounts(Guid.NewGuid(), employee,
            allowCompanyObserver: true).ToArrayAsync());
        Assert.Empty(await db.QueryInternalConversationNotificationUnreadCounts(company, Guid.NewGuid(),
            allowCompanyObserver: true).ToArrayAsync());
        Assert.Empty(await db.QueryInternalConversationNotificationUnreadCounts(company, employee, [],
            allowCompanyObserver: true).ToArrayAsync());
        Assert.Equal(0, await db.InternalConversationParticipants.CountAsync());
    }
}
