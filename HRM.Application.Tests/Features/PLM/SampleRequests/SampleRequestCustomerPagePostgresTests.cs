using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.Models;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Features.PLM.SampleRequests.Services;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestCustomerPagePostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task CustomerPageIncludesWholeMatchingGroupAndNeverExpandsOutsideFilteredScope()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var customerEntity = db.Model.FindEntityType(typeof(Customer))!;
        var requestEntity = db.Model.FindEntityType(typeof(SampleRequest))!;
        await CreateTable(connection, customerEntity);
        await CreateTable(connection, requestEntity);
        var company = Guid.NewGuid(); var owner = Guid.NewGuid();
        var firstCustomer = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondCustomer = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var filteredCustomer = Guid.NewGuid();
        foreach (var customer in new[] { firstCustomer, secondCustomer, filteredCustomer })
            await Insert(connection, customerEntity, new()
            {
                [nameof(Customer.CustomerId)] = customer, [nameof(Customer.CompanyId)] = company,
                // Same display name and code must remain separate customers.
                [nameof(Customer.CustomerName)] = "Customer", [nameof(Customer.ExternalId)] = "KH-SAME"
            });
        var firstIds = new List<Guid>(); var secondIds = new List<Guid>();
        for (var index = 0; index < 7; index++)
        {
            var id = Guid.NewGuid(); var customer = index % 3 == 0 ? secondCustomer : firstCustomer;
            (customer == firstCustomer ? firstIds : secondIds).Add(id);
            await InsertRequest(id, customer, company, owner, "InProgress", true, index);
        }
        // All exclusions include a customer that is otherwise visible, so expansion must reuse the filtered query.
        await InsertRequest(Guid.NewGuid(), firstCustomer, Guid.NewGuid(), owner, "InProgress", true, 20);
        await InsertRequest(Guid.NewGuid(), firstCustomer, company, Guid.NewGuid(), "InProgress", true, 21);
        await InsertRequest(Guid.NewGuid(), firstCustomer, company, owner, "Completed", true, 22);
        await InsertRequest(Guid.NewGuid(), firstCustomer, company, owner, "InProgress", false, 23);
        await InsertRequest(Guid.NewGuid(), filteredCustomer, company, owner, "Completed", true, 24);

        var filtered = db.SampleRequests.AsNoTracking()
            .Where(x => x.CompanyId == company && x.ManagerBy == owner && x.IsActive && x.Status == "InProgress")
            .Select(x => new SampleRequestSampleTrialReportRow { SampleRequest = x });
        Assert.Equal(2, await filtered.Select(x => x.SampleRequest.CustomerId).Distinct().CountAsync());
        var firstPage = SampleRequestCustomerPageQuery.Apply(filtered, 1, 1);
        Assert.Contains("LIMIT", firstPage.ToQueryString());
        Assert.Equal(firstIds.Order(), (await firstPage.Select(x => x.SampleRequest.SampleRequestId).ToArrayAsync()).Order());
        Assert.All(await firstPage.Select(x => x.SampleRequest.CustomerId).ToArrayAsync(), id => Assert.Equal(firstCustomer, id));
        var secondPage = SampleRequestCustomerPageQuery.Apply(filtered, 2, 1);
        Assert.Equal(secondIds.Order(), (await secondPage.Select(x => x.SampleRequest.SampleRequestId).ToArrayAsync()).Order());
        Assert.Empty(await SampleRequestCustomerPageQuery.Apply(filtered, 3, 1).Select(x => x.SampleRequest.SampleRequestId).ToArrayAsync());
        Assert.Equal(firstIds.Count + secondIds.Count,
            await SampleRequestCustomerPageQuery.Apply(filtered, 1, 2).CountAsync());
        // A request/keyword filter still selects matching requests only, not every request of that customer.
        var keywordFiltered = filtered.Where(x => x.SampleRequest.ExternalId == "TP_1");
        Assert.Single(await SampleRequestCustomerPageQuery.Apply(keywordFiltered, 1, 1)
            .Select(x => x.SampleRequest.SampleRequestId).ToArrayAsync());
        var prioritySource = db.SampleRequests.AsNoTracking()
            .Where(x => x.CompanyId == company && x.ManagerBy == owner && x.IsActive && x.Status == "InProgress")
            .Select(x => new SampleRequestSampleTrialReportRow
            {
                SampleRequest = x,
                WorkDecision = new SampleWorkDecision { Stage = "stage", Code = "action", Role = "Lab" }
            });
        var ranked = SampleRequestWorkSortQuery.Enrich(prioritySource, SampleRequestWorkAudience.Lab,
            new DateTime(2026, 11, 6), true, false, false, false, [], db.SampleRequests.Where(_ => false));
        Assert.Equal(secondCustomer, await SampleRequestWorkSortQuery.OrderPriority(ranked)
            .Select(x => x.SampleRequest.CustomerId).FirstAsync());
        var priorityCustomerIds = await SampleRequestCustomerPageQuery.SelectPriorityCustomerIdsAsync(ranked, 1, 1, CancellationToken.None);
        var urgentGroup = ranked.Where(x => priorityCustomerIds.Contains(x.SampleRequest.CustomerId));
        Assert.Equal(secondIds.Count, await urgentGroup.CountAsync());
        Assert.All(await urgentGroup.Select(x => x.SampleRequest.CustomerId).ToArrayAsync(), id => Assert.Equal(secondCustomer, id));
        Assert.Equal(new[] { firstCustomer }, await SampleRequestCustomerPageQuery.SelectCodePageCustomerIds(ranked, 1, 1).ToArrayAsync());
        await db.Customers.Where(x => x.CustomerId == firstCustomer).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExternalId, "KH_200"));
        await db.Customers.Where(x => x.CustomerId == secondCustomer).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExternalId, "KH_100"));
        Assert.Equal(new[] { secondCustomer }, await SampleRequestCustomerPageQuery.SelectCodePageCustomerIds(ranked, 1, 1).ToArrayAsync());
        Assert.Equal(secondCustomer, await SampleRequestWorkSortQuery.OrderCustomerCode(ranked)
            .Select(x => x.SampleRequest.CustomerId).FirstAsync());

        async Task InsertRequest(Guid id, Guid customer, Guid tenant, Guid manager, string status, bool active, int index)
            => await Insert(connection, requestEntity, new()
            {
                [nameof(SampleRequest.SampleRequestId)] = id, [nameof(SampleRequest.CustomerId)] = customer,
                [nameof(SampleRequest.CompanyId)] = tenant, [nameof(SampleRequest.ManagerBy)] = manager,
                [nameof(SampleRequest.Status)] = status, [nameof(SampleRequest.IsActive)] = active,
                [nameof(SampleRequest.ExternalId)] = "TP_" + index,
                [nameof(SampleRequest.CreatedDate)] = new DateTime(2026, 11, 2).AddHours(index),
                [nameof(SampleRequest.ExpectedDeliveryDate)] = customer == secondCustomer
                    ? new DateTime(2026, 11, 5) : new DateTime(2026, 11, 10)
            });
    }
}
