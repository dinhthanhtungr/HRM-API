using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.Reports.Lab.Samples;
using HRM.Application.Features.Reports.Lab.Samples.Queries.GetLabSampleReport;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Text.Json;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Application.Tests.Features.Reports;

public sealed class LabSampleReportTests
{
    [Fact]
    public async Task OverviewOctoberAndTrendSixMonthsUseIndependentCohortsWithInclusiveTo()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        SampleRequest Sample(DateTime date, string type = "Sample") => new()
        {
            SampleRequestId = Guid.NewGuid(), ProductId = Guid.NewGuid(), CompanyId = company,
            CreatedDate = date, Status = "InProgress", IsActive = true, RequestType = type,
            Customer = new Customer { CompanyId = company, IsActive = true, ExternalId = "KH_TEST" },
            Product = new Product { CompanyId = company }, ManagerByNavigation = new Employee()
        };
        store.Seed("SampleRequests", Sample(new(2026, 4, 1)), Sample(new(2026, 9, 1)),
            Sample(new(2026, 9, 30, 23, 59, 59)), Sample(new(2026, 10, 1)),
            Sample(new(2026, 10, 12, 23, 59, 59)), Sample(new(2026, 10, 13)),
            Sample(new(2026, 9, 10), "Other"));
        var handler = new GetLabSampleReportQueryHandler(db, new ScopeVisibility(company), new User(company));
        var response = await handler.Handle(new()
        {
            From = new(2026, 10, 1), To = new(2026, 10, 12), RequestType = "Sample"
        }, default);
        Assert.True(response.Success);
        var data = response.Data!;
        Assert.Equal("Asia/Ho_Chi_Minh", data.Timezone);
        Assert.Equal(new LabSamplePeriodDto(new(2026, 10, 1), new(2026, 10, 12)), data.OverviewPeriod);
        Assert.Equal(new LabSampleTrendPeriodDto(new(2026, 5, 1), new(2026, 10, 12), 6), data.TrendPeriod);
        Assert.Equal(2, data.Overview.RequestCount);
        Assert.Equal(6, data.Months.Count);
        Assert.Equal(new[] { 5, 6, 7, 8, 9, 10 }, data.Months.Select(x => x.Month));
        Assert.Equal(2, data.Months[4].Metrics.RequestCount);
        Assert.Equal(2, data.Months[5].Metrics.RequestCount);
        Assert.Equal(new DateOnly(2026, 9, 30), data.Months[4].PeriodTo);
        Assert.False(data.Months[4].IsPartialMonth);
        Assert.Equal(new DateOnly(2026, 10, 12), data.Months[5].PeriodTo);
        Assert.True(data.Months[5].IsPartialMonth);
        Assert.Equal("available", data.Months[4].DataStatus);
        Assert.Equal("no_requests", data.Months[0].DataStatus);
        Assert.Null(data.Months[0].DataUnavailableReason);
        Assert.Null(data.Months[0].Metrics.WinPercentage);
        Assert.Equal("no_requests", data.Months[0].Metrics.WinUnavailableReason);
        Assert.Equal("no_valid_dates", data.Overview.LeadTimeUnavailableReason);
        Assert.Equal(2, data.Overview.LeadTimeEligibleCount);

        // Một kỳ overview dài hơn trend vẫn có cohort riêng, không cộng 6 tháng để dựng KPI.
        var wider = await handler.Handle(new()
        {
            From = new(2026, 4, 1), To = new(2026, 10, 12), RequestType = "Sample"
        }, default);
        Assert.Equal(5, wider.Data!.Overview.RequestCount);
        Assert.Equal(4, wider.Data.Months.Sum(x => x.Metrics.RequestCount));
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 1, 31)]
    [InlineData(2024, 2, 29)]
    public void CalendarMonthsIncludeYearBoundaryLeapYearAndCompleteLastMonth(int year, int month, int day)
    {
        var to = new DateOnly(year, month, day);
        var months = LabSampleReportPeriods.BuildMonths(to, []);
        Assert.Equal(6, months.Count);
        Assert.False(months[^1].IsPartialMonth);
        Assert.Equal(to, months[^1].PeriodTo);
        Assert.Equal(LabSampleReportPeriods.TrendStart(to), months[0].PeriodFrom);
        Assert.All(months, x => Assert.Equal("no_requests", x.DataStatus));
    }

    [Fact]
    public void DateQualityMetadataDistinguishesRealZeroFromUnavailable()
    {
        var zero = LabSampleAggregation.ToDto(new() { Count = 2, Valid = 1, Missing = 1, Average = 0 });
        Assert.Equal(0d, zero.DevelopmentLeadTimeDays);
        Assert.Null(zero.LeadTimeUnavailableReason);
        Assert.Equal(zero.LeadTimeValidCount + zero.LeadTimeMissingCount + zero.LeadTimeInvalidCount, zero.LeadTimeEligibleCount);
        var missing = LabSampleAggregation.ToDto(new() { Count = 2, Missing = 1, Invalid = 1 });
        Assert.Null(missing.DevelopmentLeadTimeDays);
        Assert.Equal("no_valid_dates", missing.LeadTimeUnavailableReason);
        Assert.Equal(2, missing.LeadTimeEligibleCount);
        var empty = LabSampleAggregation.ToDto(null);
        Assert.Equal("no_requests", empty.LeadTimeUnavailableReason);
        Assert.Equal(0, empty.LeadTimeEligibleCount);
        Assert.Null(empty.DevelopmentLeadTimeDays);
    }
    [Fact]
    public async Task AuditCutoff_RejectsLaterConversionsAndOtherCompanyEvidence()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var sample = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), ProductId = Guid.NewGuid(), CompanyId = company,
            CreatedDate = new(2026, 10, 1), Status = "Completed", IsActive = true,
            Customer = new Customer { CompanyId = company, IsActive = true, ExternalId = "KH_TEST" },
            Product = new Product { CompanyId = company }, ManagerByNavigation = new Employee()
        };
        var outsider = new SampleRequest { CompanyId = Guid.NewGuid(), IsActive = true };
        store.Seed("SampleRequests", sample, outsider);
        store.Seed("AuditLogs",
            new AuditLog { CompanyId = company, SchemaName = "SampleRequests", TableName = "SampleRequests",
                RecordId = sample.SampleRequestId, ChangedAt = new(2026, 10, 13),
                OldValues = JsonDocument.Parse("{\"Status\":\"SampleSent\"}"), NewValues = JsonDocument.Parse("{\"Status\":\"Completed\"}") },
            new AuditLog { CompanyId = Guid.NewGuid(), SchemaName = "SampleRequests", TableName = "SampleRequests",
                RecordId = sample.SampleRequestId, ChangedAt = new(2026, 10, 11),
                OldValues = JsonDocument.Parse("{\"Status\":\"SampleSent\"}"), NewValues = JsonDocument.Parse("{\"Status\":\"Completed\"}") },
            new AuditLog { CompanyId = company, SchemaName = "SampleRequests", TableName = "Products",
                RecordId = sample.ProductId, ChangedAt = new(2026, 10, 2),
                OldValues = JsonDocument.Parse("{\"ColourCode\":null}"), NewValues = JsonDocument.Parse("{\"ColourCode\":\"TP1\"}") },
            new AuditLog { CompanyId = company, SchemaName = "SampleRequests", TableName = "Products",
                RecordId = sample.ProductId, ChangedAt = new(2026, 10, 9),
                OldValues = JsonDocument.Parse("{\"ColourCode\":\"TP1\"}"), NewValues = JsonDocument.Parse("{\"ColourCode\":\"TP2\"}") });
        var query = await new LabSampleReportReader(db, new ScopeVisibility(company)).QueryAsync(
            new() { From = new(2026, 10, 1), To = new(2026, 10, 12) }, default);
        var row = Assert.Single(query.ToList());
        Assert.Equal(new DateTime(2026, 10, 2), row.ColourAssignedAt);
        Assert.Null(row.CustomerApprovedAt);
        Assert.False(row.ConversionUnknown);
        Assert.False(LabSampleReportRules.Conversion(row));
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [LabReportPostgresFact]
    public async Task ReadOnlyPostgres_ReconcilesOverviewMonthlyAndPagedDrawer()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("HRM_LAB_REPORT_TEST_CONNECTION")!).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '30s'");
        var company = await db.SampleRequests.GroupBy(x => x.CompanyId).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstAsync();
        var visibility = new ScopeVisibility(company);
        var rows = await new LabSampleReportReader(db, visibility).QueryAsync(
            new() { From = new(2026, 5, 1), To = new(2026, 10, 12) }, default);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var totals = LabSampleAggregation.ToDto(await rows.GroupBy(x => 1).Select(LabSampleAggregation.Projection).SingleOrDefaultAsync());
        var overviewMs = timer.ElapsedMilliseconds;
        timer.Restart();
        var months = await rows.GroupBy(x => x.CreatedAt.Year * 100 + x.CreatedAt.Month).Select(LabSampleAggregation.Projection).ToListAsync();
        var monthlyMs = timer.ElapsedMilliseconds;
        Assert.Equal(totals.RequestCount, months.Sum(x => x.Count));
        Assert.Equal(totals.WinNumerator, months.Sum(x => x.Won));
        var combined = LabSampleAggregation.ToDto(LabSampleAggregation.Combine(months));
        Assert.Equal(totals.DevelopmentLeadTimeDays, combined.DevelopmentLeadTimeDays);
        Assert.Equal(totals.WinPercentage, combined.WinPercentage);
        var report = await new GetLabSampleReportQueryHandler(db, visibility, new User(company)).Handle(new()
        {
            From = new(2026, 10, 1), To = new(2026, 10, 12)
        }, default);
        Assert.True(report.Success);
        Assert.Equal(6, report.Data!.Months.Count);
        Assert.Equal(months.FirstOrDefault(x => x.Key == 202609)?.Count ?? 0, report.Data.Months[4].Metrics.RequestCount);
        Assert.Equal(months.FirstOrDefault(x => x.Key == 202610)?.Count ?? 0, report.Data.Overview.RequestCount);
        Assert.Equal(totals.RequestCount, report.Data.Months.Sum(x => x.Metrics.RequestCount));
        Assert.Equal(report.Data.Overview.RequestCount, report.Data.Overview.LeadTimeEligibleCount);
        timer.Restart();
        Assert.Equal(totals.WinNumerator, await rows.Where(LabSampleReportRules.Predicate(LabSampleMetric.Won)).CountAsync());
        Assert.Equal(totals.LeadTimeValidCount, await rows.Where(LabSampleReportRules.Predicate(LabSampleMetric.LeadTimeValid)).CountAsync());
        Assert.Equal(totals.LeadTimeMissingCount, await rows.Where(LabSampleReportRules.Predicate(LabSampleMetric.LeadTimeMissing)).CountAsync());
        Assert.Equal(totals.LeadTimeInvalidCount, await rows.Where(LabSampleReportRules.Predicate(LabSampleMetric.LeadTimeInvalid)).CountAsync());
        var page = await rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.SampleRequestId).Take(20).ToListAsync();
        Assert.Equal(page.Count, page.Select(x => x.SampleRequestId).Distinct().Count());
        Console.WriteLine($"Lab report readonly: requests={totals.RequestCount}, valid={totals.LeadTimeValidCount}, missing={totals.LeadTimeMissingCount}, invalid={totals.LeadTimeInvalidCount}, unknownConversion={totals.ConversionUnknownCount}, overviewMs={overviewMs}, monthlyMs={monthlyMs}, drawerChecksMs={timer.ElapsedMilliseconds}");
        await tx.RollbackAsync();
    }
    [Fact]
    public void MultipleTrialsDoNotMultiplyRequestsOrCustomerApprovalWins()
    {
        var date = new DateTime(2026, 10, 12);
        var rows = new[]
        {
            new LabSampleRowDto { Status = "Completed", TrialCount = 4, CustomerApprovedAt = date,
                ColourAssignedAt = date.AddDays(-2), LabApprovedAt = date, Accepted = true, Rejected = true },
            new LabSampleRowDto { Status = "InProgress", TrialCount = 2 }
        }.AsQueryable();
        var totals = LabSampleAggregation.ToDto(rows.GroupBy(x => 1).Select(LabSampleAggregation.Projection).Single());
        Assert.Equal(2, totals.RequestCount);
        Assert.Equal(1, totals.WinNumerator);
        Assert.Equal(50m, totals.WinPercentage);
        Assert.Equal(1, rows.Where(LabSampleReportRules.Predicate(LabSampleMetric.Won)).Count());
        Assert.Equal(1, totals.LeadTimeMissingCount);
        Assert.Equal(2d, totals.DevelopmentLeadTimeDays);
    }

    [Fact]
    public void MissingAndReversedDatesRemainSeparateAndDoNotEnterAverage()
    {
        var date = new DateTime(2026, 10, 12);
        var rows = new[]
        {
            new LabSampleRowDto { ColourAssignedAt = date, LabApprovedAt = date.AddDays(-1) },
            new LabSampleRowDto { LabApprovedAt = date },
            new LabSampleRowDto { ColourAssignedAt = date, LabApprovedAt = date.AddHours(12) }
        }.AsQueryable();
        var totals = LabSampleAggregation.ToDto(rows.GroupBy(x => 1).Select(LabSampleAggregation.Projection).Single());
        Assert.Equal(1, totals.LeadTimeInvalidCount);
        Assert.Equal(1, totals.LeadTimeMissingCount);
        Assert.Equal(1, totals.LeadTimeValidCount);
        Assert.Equal(0.5d, totals.DevelopmentLeadTimeDays);
        Assert.Null(LabSampleReportRules.LeadTime(date, date.AddDays(-1)));
        Assert.Equal(totals.LeadTimeInvalidCount, rows.Where(LabSampleReportRules.Predicate(LabSampleMetric.LeadTimeInvalid)).Count());
    }

    [Fact]
    public void UnknownHistoricalCompletionAndEmptyPeriodReturnNullNotFakeZero()
    {
        var row = new LabSampleRowDto { Status = "Completed", ConversionUnknown = true };
        var total = LabSampleAggregation.ToDto(new[] { row }.AsQueryable().GroupBy(x => 1).Select(LabSampleAggregation.Projection).Single());
        Assert.Null(total.WinPercentage);
        Assert.Null(LabSampleReportRules.Conversion(row));
        Assert.Equal("missing_completed_audit", total.WinUnavailableReason);
        var empty = LabSampleAggregation.ToDto(null);
        Assert.Equal(0, empty.RequestCount);
        Assert.Null(empty.WinPercentage);
        Assert.Null(empty.DevelopmentLeadTimeDays);
        Assert.False(LabSampleReportRules.Conversion(new LabSampleRowDto { Status = "Completed" }));
    }

    [Fact]
    public void MonthlyLeadTimeIsWeightedByValidRequestsBeforeRounding()
    {
        var result = LabSampleAggregation.ToDto(LabSampleAggregation.Combine(
            [new() { Count = 1, Valid = 1, Average = 10 }, new() { Count = 9, Valid = 9, Average = 1 }]));
        Assert.Equal(1.9d, result.DevelopmentLeadTimeDays);
        Assert.Equal(10, result.LeadTimeValidCount);
    }

    [Fact]
    public async Task PostgresProjectionAndAggregationTranslateWithoutPriceOrOrderMatching()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var rows = await new LabSampleReportReader(db, new ScopeVisibility())
            .QueryAsync(new() { From = new(2026, 10, 1), To = new(2026, 10, 12) }, default);
        var sql = rows.GroupBy(x => 1).Select(LabSampleAggregation.Projection).ToQueryString();
        Assert.Contains("AuditLogs", sql);
        Assert.Contains("FormulaVersions", sql);
        Assert.Contains("2026-10-13", sql);
        Assert.DoesNotContain("MerchandiseOrders", sql);
        Assert.DoesNotContain("TotalPrice", sql);
        var drawer = rows.Where(LabSampleReportRules.Predicate(LabSampleMetric.Won)).OrderBy(x => x.CreatedAt).Take(20).ToQueryString();
        Assert.Contains("LIMIT", drawer);
    }

    [Fact]
    public async Task CompanyMismatchFailsBeforeQueryingVisibilityOrDatabase()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var visibility = new ScopeVisibility();
        var user = new User();
        var result = await new GetLabSampleReportQueryHandler(db, visibility, user).Handle(
            new() { CompanyId = Guid.NewGuid(), From = new(2026, 10, 1), To = new(2026, 10, 12) }, default);
        Assert.False(result.Success);
        Assert.Equal(0, visibility.Calls);
    }

    private sealed class User(Guid? companyId = null) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = companyId ?? Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }

    private sealed class ScopeVisibility(Guid? fullCompany = null) : ICustomerVisibilityService
    {
        public int Calls { get; private set; }
        private readonly Guid companyId = fullCompany ?? Guid.NewGuid();
        public Task<ViewerScope> BuildScopeAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ViewerScope(companyId, Guid.NewGuid(), false, false,
                new HashSet<Guid>(), new HashSet<Guid>(), DateTime.Now));
        }
        public IQueryable<SampleRequest> ApplySampleRequestVisibility(IQueryable<SampleRequest> q, IQueryable<Customer> customers, ViewerScope scope)
            => fullCompany.HasValue ? q.Where(x => x.CompanyId == scope.CompanyId)
                : q.Where(x => x.CompanyId == scope.CompanyId && x.CustomerId == scope.EmployeeId);
        public IQueryable<Customer> ApplyCustomerVisibility(IQueryable<Customer> q, ViewerScope s) => throw new NotSupportedException();
        public IQueryable<Customer> ApplyCustomerVisibilityIncludingInactive(IQueryable<Customer> q, ViewerScope s) => throw new NotSupportedException();
        public IQueryable<MerchandiseOrder> ApplyMerchandiseOrderVisibility(IQueryable<MerchandiseOrder> q, IQueryable<Customer> c, ViewerScope s) => throw new NotSupportedException();
        public IQueryable<Quotation> ApplyQuotationVisibility(IQueryable<Quotation> q, IQueryable<Customer> c, ViewerScope s) => throw new NotSupportedException();
    }
}

public sealed class LabReportPostgresFactAttribute : FactAttribute
{
    public LabReportPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HRM_LAB_REPORT_TEST_CONNECTION")))
            Skip = "Set HRM_LAB_REPORT_TEST_CONNECTION for read-only PostgreSQL reconciliation and timing.";
    }
}
