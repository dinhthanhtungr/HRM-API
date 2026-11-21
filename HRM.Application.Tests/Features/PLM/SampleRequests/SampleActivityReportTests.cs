using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestAdminSummary;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.SampleRequests;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleActivityDetails;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleActivityReportTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData("other", "unknown", "multiple", true)]
    [InlineData("sample", "responses", "QAQC.MAU", true)]
    [InlineData("bad", "accepted", null, false)]
    [InlineData("chip", "bad", null, false)]
    [InlineData(null, null, "bad", false)]
    public void RejectsUnsupportedDetailFilters(string? kind, string? outcome, string? team, bool expected)
        => Assert.Equal(expected, new GetSampleActivityDetailsQuery { Kind = kind, Outcome = outcome, Team = team }.HasValidFilters());
    [Fact]
    public void AggregatesActivitiesWithoutCountingFeedbackAsExtraWork()
    {
        var counts = SampleActivityReportReader.Aggregate([
            new(Guid.NewGuid(), "A", Guid.NewGuid(), "sample", "accepted", 2, 5),
            new(Guid.NewGuid(), "B", Guid.NewGuid(), "sample", "waiting", 1, 0),
            new(Guid.NewGuid(), "B", null, "sample", "cancelled", 1, 1)]);
        var value = counts["sample"];
        Assert.Equal(4, value.Completed);
        Assert.Equal(6, value.Responses);
        Assert.Equal(value.Completed, value.Accepted + value.Rejected + value.Waiting + value.Cancelled + value.Unknown);
    }

    [PostgresGroupStorageFact]
    public async Task CountsEveryDeliveryAndPriceRoundWithLatestFeedbackBeyondThePeriod()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(Company), typeof(Employee), typeof(Group), typeof(MemberInGroup), typeof(Product), typeof(Formula), typeof(SampleRequest), typeof(SampleRequestSampleTrial), typeof(ProductPricingVersion), typeof(SampleRequestWorkflowEvent) })
            await SampleWorkDecisionPostgresTests.CreateTable(connection, db.Model.FindEntityType(type)!);
        var company = Guid.NewGuid(); var request = Guid.NewGuid(); var product = Guid.NewGuid(); var formula = Guid.NewGuid();
        var lab = Guid.NewGuid(); var branch = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var chip = Guid.NewGuid(); var sample = Guid.NewGuid();
        DateTime Day(int day) => new(2026, 11, day, 10, 0, 0);
        async Task Insert<T>(Dictionary<string, object?> values) => await SampleWorkDecisionPostgresTests.Insert(connection, db.Model.FindEntityType(typeof(T))!, values);
        await Insert<Employee>(new() { ["EmployeeId"] = lab, ["CompanyId"] = company, ["IsActive"] = true, ["FullName"] = "Lab A" });
        await Insert<Product>(new() { ["ProductId"] = product, ["CompanyId"] = company, ["IsActive"] = true, ["Name"] = "Product A" });
        await Insert<Formula>(new() { ["FormulaId"] = formula, ["CompanyId"] = company, ["ProductId"] = product, ["IsActive"] = true, ["ExternalId"] = "VU_1" });
        var rd = Guid.NewGuid();
        await Insert<Group>(new() { ["GroupId"] = rd, ["CompanyId"] = company, ["GroupType"] = "QAQC.RD" });
        await Insert<MemberInGroup>(new() { ["MemberId"] = Guid.NewGuid(), ["GroupId"] = rd, ["Profile"] = lab, ["IsActive"] = true });
        await Insert<SampleRequest>(new() { ["SampleRequestId"] = request, ["CompanyId"] = company, ["ProductId"] = product,
            ["BranchId"] = branch, ["CreatedDate"] = Day(1), ["IsActive"] = true, ["Status"] = "SampleSent", ["ExternalId"] = "TP_1" });
        foreach (var (id, day, active) in new[] { (first, 3, false), (second, 6, true) })
            await Insert<ProductPricingVersion>(new() { ["ProductPricingVersionId"] = id, ["CompanyId"] = company,
                ["ProductId"] = product, ["SourceFormulaId"] = formula, ["ApprovedAt"] = Day(day), ["Status"] = ProductPricingStatus.Approved,
                ["Currency"] = "VND", ["IsActive"] = active, ["StandardSellingPrice"] = 100m });
        foreach (var (id, day, qty, status) in new[] { (chip, 3, 0m, SampleTrialStatus.Approved), (sample, 5, 2m, SampleTrialStatus.Failed), (Guid.NewGuid(), 11, 3m, SampleTrialStatus.WaitingCustomerFeedback) })
            await Insert<SampleRequestSampleTrial>(new() { ["SampleRequestSampleTrialId"] = id, ["SampleRequestId"] = request,
                ["FormulaId"] = formula, ["SentDate"] = Day(day), ["DeliveredSampleQuantityKg"] = qty, ["Status"] = status,
                ["IsActive"] = true, ["SentByEmployeeId"] = lab });
        async Task Event(string type, int day, Guid? version = null, Guid? trial = null, Guid? eventCompany = null)
            => await Insert<SampleRequestWorkflowEvent>(new() { ["Id"] = Guid.NewGuid(), ["CompanyId"] = eventCompany ?? company,
                ["SampleRequestId"] = request, ["FormulaId"] = formula, ["ProductPricingVersionId"] = version,
                ["SampleRequestSampleTrialId"] = trial, ["EventType"] = type, ["RecordedAt"] = Day(day), ["RecordedByEmployeeId"] = lab });
        await Event("LabFormulaConfirmed", 2);
        await Event("CustomerPriceAccepted", 4, first);
        await Event("CustomerPriceRejected", 5, first);
        await Event("CustomerPriceAccepted", 11, first);
        await Event("CustomerPriceRejected", 7);
        await Event("CustomerPriceFeedbackShared", 7, second);
        await Event("CustomerPriceAccepted", 8, second, eventCompany: Guid.NewGuid());
        await Event("CustomerFeedbackRecorded", 4, trial: chip);
        await Event("CustomerFeedbackRecorded", 11, trial: chip);
        await Event("CustomerFeedbackRecorded", 6, trial: sample);
        var visible = db.SampleRequests.Where(x => x.CompanyId == company && x.IsActive);
        var query = SampleActivityQuery.Build(visible, db, db.ProductPricingVersions, Day(1).Date, Day(11).Date, Day(12));
        var rows = await query.ToListAsync();
        Assert.Equal(4, rows.Count);
        Assert.Equal("accepted", rows.Single(x => x.Id == first).Outcome);
        Assert.Equal(3, rows.Single(x => x.Id == first).Responses);
        Assert.Equal("rejected", rows.Single(x => x.Id == second).Outcome);
        Assert.Equal(1, rows.Single(x => x.Id == second).Responses);
        Assert.Equal(2, rows.Single(x => x.Id == chip).Responses);
        Assert.All(rows, row => Assert.Equal(lab, row.EmployeeId));
        var counts = await query.GroupBy(x => new { x.Kind, x.Outcome }).Select(g => new { g.Key, Count = g.Count(), Responses = g.Sum(x => x.Responses) }).ToListAsync();
        Assert.Equal(4, counts.Sum(x => x.Count));
        Assert.Equal(7, counts.Sum(x => x.Responses));
        Assert.Equal(2, await query.Where(x => x.Outcome == "accepted").CountAsync());
        Assert.Single(await query.OrderBy(x => x.Id).Skip(1).Take(1).ToListAsync());
        var report = await SampleActivityReportReader.ReadAsync(query, db, db, company, Day(12), default);
        Assert.Equal(2, report.Totals["quotation"].Completed);
        Assert.Equal("QAQC.RD", Assert.Single(report.Employees).Team);
        Assert.Equal(4, await GetSampleActivityDetailsQueryHandler.Filter(query, db, company, new() { Team = "QAQC.RD", EmployeeId = lab, BranchId = branch }).CountAsync());
        Assert.Equal(2, await GetSampleActivityDetailsQueryHandler.Filter(query, db, company, new() { Kind = "quotation", Outcome = "responses" }).CountAsync());
        var page = await GetSampleActivityDetailsQueryHandler.SelectPage(query, visible, db, company, 2, 1).ToListAsync();
        Assert.Equal("TP_1", Assert.Single(page).RequestCode);
        Assert.Equal("VU_1", page[0].FormulaCode);
        var color = Guid.NewGuid();
        await Insert<Group>(new() { ["GroupId"] = color, ["CompanyId"] = company, ["GroupType"] = "QAQC.MAU" });
        await Insert<MemberInGroup>(new() { ["MemberId"] = Guid.NewGuid(), ["GroupId"] = color, ["Profile"] = lab, ["IsActive"] = true });
        report = await SampleActivityReportReader.ReadAsync(query, db, db, company, Day(12), default);
        Assert.Equal("multiple", Assert.Single(report.Employees).Team);
        Assert.Equal(4, await GetSampleActivityDetailsQueryHandler.Filter(query, db, company, new() { Team = "multiple" }).CountAsync());
        Assert.Equal(0, await GetSampleActivityDetailsQueryHandler.Filter(query, db, company, new() { Team = "QAQC.RD" }).CountAsync());
        Assert.Empty(await SampleActivityQuery.Build(db.SampleRequests.Where(x => x.CompanyId == Guid.Empty), db, db.ProductPricingVersions, Day(1), Day(11), Day(12)).ToListAsync());
        await Event("CustomerPriceAcceptanceRevoked", 12, first);
        Assert.Equal("waiting", (await query.SingleAsync(x => x.Id == first)).Outcome);
        var pending = Guid.NewGuid();
        await Insert<SampleRequestSampleTrial>(new() { ["SampleRequestSampleTrialId"] = pending, ["SampleRequestId"] = request,
            ["FormulaId"] = formula, ["SentDate"] = Day(9), ["DeliveredSampleQuantityKg"] = 0m, ["Status"] = SampleTrialStatus.WaitingCustomerFeedback,
            ["IsActive"] = true, ["SentByEmployeeId"] = Guid.NewGuid() });
        Assert.Null((await query.SingleAsync(x => x.Id == pending)).EmployeeId);
        await visible.ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "Cancelled"));
        Assert.Equal("cancelled", (await query.SingleAsync(x => x.Id == pending)).Outcome);
        Assert.Equal("accepted", (await query.SingleAsync(x => x.Id == chip)).Outcome);
        var unknown = Guid.NewGuid();
        await Insert<SampleRequestSampleTrial>(new() { ["SampleRequestSampleTrialId"] = unknown, ["SampleRequestId"] = request,
            ["SentDate"] = Day(8), ["Status"] = SampleTrialStatus.SampleSent, ["IsActive"] = true });
        Assert.Equal("other", (await query.SingleAsync(x => x.Id == unknown)).Kind);
        Assert.Single(await GetSampleActivityDetailsQueryHandler.Filter(query, db, company, new() { Kind = "other" }).ToListAsync());

    }
}
