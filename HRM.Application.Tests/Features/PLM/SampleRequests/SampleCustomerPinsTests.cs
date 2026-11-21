using HRM.Application.Features.PLM.SampleRequests.PersonalWork;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleCustomerPinsTests
{
    [Fact]
    public void RequestAcceptsFrontendAudienceAndRequiresExplicitPinFlag()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var request = System.Text.Json.JsonSerializer.Deserialize<SaveSampleCustomerPinsRequest>("{\"isPinned\":true,\"audience\":\"labAdmin\"}", options)!;
        Assert.Equal(SampleRequestWorkAudience.LabAdmin, request.Audience);
        Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<SaveSampleCustomerPinsRequest>("{\"audience\":\"sale\"}", options));
    }

    [Fact]
    public async Task InvalidAudienceAndAnonymousUserFailBeforePersistence()
    {
        foreach (var authenticated in new[] { true, false })
        {
            var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser", authenticated);
            var result = await new SaveSampleCustomerPinsCommandHandler(null!, null!, user, null!, null!)
                .Handle(new(Guid.NewGuid(), new(true, (SampleRequestWorkAudience)999)), default);
            Assert.False(result.Success);
        }
    }

    [Fact]
    public void CustomerQueryRetainsCompanyActiveCustomerAndCutoffScope()
    {
        var company = Guid.NewGuid(); var customer = Guid.NewGuid();
        SampleRequest Make(Guid companyId, Guid customerId, DateTime created, bool active = true) => new()
        {
            CompanyId = companyId, CustomerId = customerId, CreatedDate = created, IsActive = active,
            Customer = new Customer { ExternalId = "KH_TEST" }
        };
        var included = Make(company, customer, new(2026, 11, 1));
        var rows = new[] { included, Make(Guid.NewGuid(), customer, new(2026, 11, 1)),
            Make(company, Guid.NewGuid(), new(2026, 11, 1)), Make(company, customer, new(2026, 10, 31)),
            Make(company, customer, new(2026, 11, 1), false) };
        Assert.Same(included, SaveSampleCustomerPinsCommandHandler.CustomerRequests(rows.AsQueryable(), company, customer).Single());
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = SaveSampleCustomerPinsCommandHandler.CustomerRequests(db.SampleRequests, company, customer).Select(x => x.SampleRequestId).ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.DoesNotContain("SampleTrials", sql);
    }

    [Fact]
    public void BulkUpsertUsesParametersAndDoesNotOverwriteNoteOrReminderFields()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var sql = SaveSampleCustomerPinsCommandHandler.Upsert(ids, Guid.NewGuid(), Guid.NewGuid(), true, DateTime.Now);
        Assert.Contains(ids, sql.GetArguments());
        Assert.Contains("unnest", sql.Format);
        var update = sql.Format.Split("DO UPDATE")[1];
        Assert.Contains("COALESCE", update);
        Assert.DoesNotContain("NotifyOnDue", update);
        Assert.DoesNotContain("ReminderTaskId", update);
        Assert.DoesNotContain("WorkTask", sql.Format);
        Assert.DoesNotContain(ids[0].ToString(), sql.Format);
    }
}
