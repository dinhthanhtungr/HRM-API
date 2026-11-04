using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleManagedSaleReadPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ManagedSaleOwnershipIntersectsNarrowerWriteVisibilityBeforeCountAndPaging()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var entity = db.Model.FindEntityType(typeof(SampleRequest))!;
        await CreateTable(connection, entity);
        var company = Guid.NewGuid(); var actor = Guid.NewGuid(); var managed = Guid.NewGuid(); var outside = Guid.NewGuid();
        var writableCustomer = Guid.NewGuid(); var readOnlyCustomer = Guid.NewGuid();
        var ownId = Guid.NewGuid(); var managedId = Guid.NewGuid(); var readOnlyId = Guid.NewGuid();
        foreach (var row in new[] {
            (ownId, company, actor, writableCustomer), (managedId, company, managed, writableCustomer),
            (readOnlyId, company, managed, readOnlyCustomer), (Guid.NewGuid(), company, outside, writableCustomer),
            (Guid.NewGuid(), Guid.NewGuid(), managed, writableCustomer) })
            await Insert(connection, entity, new() {
                [nameof(SampleRequest.SampleRequestId)] = row.Item1, [nameof(SampleRequest.CompanyId)] = row.Item2,
                [nameof(SampleRequest.ManagerBy)] = row.Item3, [nameof(SampleRequest.CustomerId)] = row.Item4,
                [nameof(SampleRequest.IsActive)] = true });
        var visible = db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive);
        var writable = visible.Where(x => x.CustomerId == writableCustomer);
        var manager = SampleRequestWorkQueueQuery.ApplySaleOwnership(visible, writable, [actor, managed]);
        Assert.Equal(2, await manager.CountAsync());
        var managerIds = await manager.OrderBy(x => x.SampleRequestId).Select(x => x.SampleRequestId).ToArrayAsync();
        Assert.Contains(ownId, managerIds); Assert.Contains(managedId, managerIds); Assert.DoesNotContain(readOnlyId, managerIds);
        Assert.Single(await manager.OrderBy(x => x.SampleRequestId).Skip(1).Take(1).Select(x => x.SampleRequestId).ToArrayAsync());
        Assert.Equal(new[] { ownId }, await SampleRequestWorkQueueQuery.ApplySaleOwnership(visible, writable, [actor])
            .Select(x => x.SampleRequestId).ToArrayAsync());
    }

}
