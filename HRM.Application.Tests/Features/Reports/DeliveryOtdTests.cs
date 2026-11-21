using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.DeliveryOtd;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdHeader;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdDetails;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Tests.Features.Reports;

public sealed class DeliveryOtdTests
{
    private static readonly DateTime Due = new(2026, 10, 10);
    private static DeliveryOtdDetailDto Line(DateTime? due) => new(Guid.NewGuid(), Guid.NewGuid(), "PO",
        Guid.NewGuid(), "Customer", Guid.NewGuid(), "Product", Due, null, due, 100, 0, 0, 0, null, null, 0,
        false, DeliveryOtdStatus.NotDue);

    [Fact]
    public void PoPauseHistory_AlwaysPasses_EvenWithoutDeliveryOrWithFuturePromise()
    {
        var summary = DeliveryOtdPoCalculator.Summarize([
            new(Guid.NewGuid(), Due, Due.AddDays(2), true),
            new(Guid.NewGuid(), Due, null, true),
            new(Guid.NewGuid(), Due.AddDays(5), null, true),
            new(Guid.NewGuid(), Due, Due.AddDays(2), false)
        ], Due.AddDays(2));
        Assert.Equal(4, summary.EvaluatedPos);
        Assert.Equal(3, summary.OnTimePos);
        Assert.Equal(1, summary.LatePos);
        Assert.Equal(0, summary.PendingPos);
        Assert.Equal(75m, summary.PoOtdPercent);
    }

    [Theory]
    [InlineData("current", true)]
    [InlineData("from", true)]
    [InlineData("to", true)]
    [InlineData("by", true)]
    [InlineData("reason", true)]
    [InlineData("type", true)]
    [InlineData("blank", false)]
    [InlineData("none", false)]
    public async Task PoReader_DetectsRetainedPauseData_WhenCurrentFlagIsOff(string marker, bool passes)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
        var company = Guid.NewGuid();
        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CompanyId = company, OrderType = OrderType.Merchandise, CreateDate = Due,
            IsDeliveryPaused = marker == "current",
            DeliveryPausedFrom = marker == "from" ? Due : null,
            DeliveryPausedTo = marker == "to" ? Due : null,
            DeliveryPausedBy = marker == "by" ? Guid.NewGuid() : null,
            DeliveryPauseReason = marker == "reason" ? "Customer hold" : marker == "blank" ? "  " : null,
            DeliveryPauseType = marker == "type" ? "Customer" : marker == "blank" ? "  " : null
        };
        var line = new MerchandiseOrderDetail
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = order.MerchandiseOrderId,
            MerchandiseOrder = order, ExpectedQuantity = 100
        };
        store.Seed(nameof(db.MerchandiseOrders), order);
        store.Seed(nameof(db.MfgOrderPOs), new MfgOrderPO
        {
            IsActive = true, Detail = line, MerchandiseOrderDetailId = line.MerchandiseOrderDetailId,
            ProductionOrder = new() { IsActive = true, CompanyId = company, ExpectedDate = Due }
        });
        var selected = Line(Due) with { OrderId = order.MerchandiseOrderId };
        var summary = await DeliveryOtdReader.ReadPoSummaryAsync(db, [selected], company, Due, default);
        Assert.Equal(passes ? 1 : 0, summary.OnTimePos);
        Assert.Equal(passes ? 0 : 1, summary.PendingPos);
        Assert.Equal(passes ? 100m : 0m, summary.PoOtdPercent);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void PoSummary_MissingPromiseAlwaysPasses_AndDueUndeliveredFails()
    {
        var summary = DeliveryOtdPoCalculator.Summarize([
            new(Guid.NewGuid(), null, null), new(Guid.NewGuid(), null, Due.AddDays(-1)),
            new(Guid.NewGuid(), Due, Due), new(Guid.NewGuid(), Due, Due.AddDays(1)),
            new(Guid.NewGuid(), Due, null), new(Guid.NewGuid(), Due.AddDays(3), null)
        ], Due.AddDays(1));
        Assert.Equal(6, summary.TotalPos);
        Assert.Equal(5, summary.EvaluatedPos);
        Assert.Equal(3, summary.OnTimePos);
        Assert.Equal(1, summary.LatePos);
        Assert.Equal(1, summary.PendingPos);
        Assert.Equal(60m, summary.PoOtdPercent);
        Assert.Null(DeliveryOtdPoCalculator.Summarize([], Due).PoOtdPercent);
    }

    [Fact]
    public async Task PoReader_UsesAllOrderLines_LatestReceipt_AndLatestPromise()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
        var company = Guid.NewGuid();
        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CompanyId = company, OrderType = OrderType.Merchandise, CreateDate = Due
        };
        MerchandiseOrderDetail Detail() => new()
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = order.MerchandiseOrderId,
            MerchandiseOrder = order, ExpectedQuantity = 100, DeliveryRequestDate = Due
        };
        var first = Detail();
        var second = Detail();
        store.Seed(nameof(db.MerchandiseOrders), order);
        store.Seed(nameof(db.MerchandiseOrderDetails), first, second);
        MfgOrderPO Link(MerchandiseOrderDetail detail, DateTime date, Guid owner) => new()
        {
            IsActive = true, Detail = detail, MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
            ProductionOrder = new() { IsActive = true, CompanyId = owner, ExpectedDate = date }
        };
        store.Seed(nameof(db.MfgOrderPOs), Link(first, Due.AddDays(-1), company), Link(second, Due, company),
            Link(second, Due.AddMonths(1), Guid.NewGuid()));
        DeliveryOrderDetail Receipt(DateTime date, string status = "Pending", bool attach = false) => new()
        {
            IsActive = true, IsAttach = attach, Quantity = 100, MerchandiseOrderDetail = second,
            MerchandiseOrderDetailId = second.MerchandiseOrderDetailId,
            DeliveryOrder = new() { CompanyId = company, CreatedDate = date, Status = status }
        };
        store.Seed(nameof(db.DeliveryOrderDetails), Receipt(Due.AddDays(-1)), Receipt(Due.AddDays(1)),
            Receipt(Due.AddMonths(1)), Receipt(Due.AddDays(2), "Canceled"), Receipt(Due.AddDays(2), attach: true));
        // Selecting one line still evaluates the whole PO; repeated lines must not duplicate the PO.
        var selected = Line(Due) with { OrderId = order.MerchandiseOrderId };
        var summary = await DeliveryOtdReader.ReadPoSummaryAsync(db, [selected, selected], company, Due.AddDays(2), default);
        Assert.Equal(1, summary.TotalPos);
        Assert.Equal(1, summary.LatePos);
        Assert.Equal(0m, summary.PoOtdPercent);
        var beforeLastReceipt = await DeliveryOtdReader.ReadPoSummaryAsync(db, [selected], company, Due, default);
        Assert.Equal(1, beforeLastReceipt.OnTimePos);
        Assert.Equal(100m, beforeLastReceipt.PoOtdPercent);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void MissingPromise_FullyDelivered_IsOnTime_InHeaderAndCharts()
    {
        var row = DeliveryOtdCalculator.Evaluate(Line(null), [new(Due, 60), new(Due.AddDays(1), 50)], Due.AddDays(1));
        Assert.Equal(DeliveryOtdStatus.OnTime, row.Status);
        Assert.Null(row.DueDate);
        Assert.Null(row.PromisedDate);
        Assert.Null(row.LateDays);
        Assert.Equal(100, row.OnTimeQuantity);
        var summary = DeliveryOtdCalculator.Summarize([row], Due.AddDays(1));
        Assert.Equal(1, summary.DueLines);
        Assert.Equal(1, summary.OnTimeLines);
        Assert.Equal(0, summary.MissingDueDateLines);
        Assert.Equal(100m, summary.OtdPercent);
        Assert.Equal(100m, summary.QuantityOtdPercent);
        var charts = DeliveryOtdCalculator.Charts([row], Due.AddDays(1));
        Assert.Equal(summary, Assert.Single(charts.Monthly).Summary);
        Assert.Equal("2026-10", Assert.Single(charts.Monthly).Key);
        Assert.Equal("OnTime", Assert.Single(charts.Statuses).Key);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public void MissingPromise_NotFullyDelivered_RemainsMissing_AndFutureReceiptIsExcluded(int delivered)
    {
        var row = DeliveryOtdCalculator.Evaluate(Line(null), [new(Due, delivered), new(Due.AddDays(1), 100)], Due);
        Assert.Equal(DeliveryOtdStatus.MissingDueDate, row.Status);
        Assert.Equal(delivered, row.DeliveredQuantity);
        Assert.Equal(0, row.OnTimeQuantity);
        var summary = DeliveryOtdCalculator.Summarize([row], Due);
        Assert.Equal(0, summary.DueLines);
        Assert.Equal(1, summary.MissingDueDateLines);
        Assert.Null(summary.OtdPercent);
        Assert.Empty(DeliveryOtdCalculator.Charts([row], Due).Monthly);
    }

    [Fact]
    public void PartialDelivery_IsNotOnTime_AndQuantityIsCapped()
    {
        var row = DeliveryOtdCalculator.Evaluate(Line(Due),
            [new(Due.AddDays(-1), 60), new(Due.AddDays(2), 60)], Due.AddDays(3));
        Assert.Equal(DeliveryOtdStatus.Late, row.Status);
        Assert.Equal(60, row.OnTimeQuantity);
        Assert.Equal(120, row.DeliveredQuantity);
        Assert.Equal(0, row.RemainingQuantity);
        Assert.Equal(2, row.LateDays);
        var summary = DeliveryOtdCalculator.Summarize([row], Due.AddDays(3));
        Assert.Equal(0m, summary.OtdPercent);
        Assert.Equal(60m, summary.QuantityOtdPercent);
    }

    [Fact]
    public void OverdueUndelivered_IsIncludedInDenominator_AndFutureIsExcluded()
    {
        var asOf = Due.AddDays(1);
        var overdue = DeliveryOtdCalculator.Evaluate(Line(Due), [], asOf);
        var future = DeliveryOtdCalculator.Evaluate(Line(Due.AddDays(5)), [], asOf);
        var missing = DeliveryOtdCalculator.Evaluate(Line(null), [], asOf);
        var summary = DeliveryOtdCalculator.Summarize([overdue, future, missing], asOf);
        Assert.Equal(1, summary.DueLines);
        Assert.Equal(1, summary.OverdueIncompleteLines);
        Assert.Equal(100, summary.OverdueRemainingQuantity);
        Assert.Equal(1, summary.MissingDueDateLines);
        Assert.Null(DeliveryOtdCalculator.Summarize([missing, future], asOf).OtdPercent);
    }

    [Fact]
    public void SameDayReceipt_IsOnTime_FutureReceiptDoesNotCount()
    {
        var row = DeliveryOtdCalculator.Evaluate(Line(Due), [new(Due.AddHours(23), 110), new(Due.AddDays(2), 20)], Due);
        Assert.Equal(DeliveryOtdStatus.OnTime, row.Status);
        Assert.Equal(100, row.OnTimeQuantity);
        Assert.Equal(110, row.DeliveredQuantity);
        Assert.Equal(100m, DeliveryOtdCalculator.Summarize([row], Due).QuantityOtdPercent);
    }

    [Fact]
    public async Task Reader_IncludesNonCanceledStatuses_ExcludesOtherCompanyCanceledAndAttachments()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
        var company = Guid.NewGuid();
        MerchandiseOrderDetail Detail(Guid owner) => new()
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), ExpectedQuantity = 100, DeliveryRequestDate = Due,
            MerchandiseOrder = new() { CompanyId = owner, OrderType = OrderType.Merchandise, CreateDate = Due }
        };
        var line = Detail(company);
        store.Seed(nameof(db.MerchandiseOrderDetails), line, Detail(Guid.NewGuid()));
        DeliveryOrderDetail Receipt(string status, Guid owner, bool attach = false) => new()
        {
            MerchandiseOrderDetailId = line.MerchandiseOrderDetailId, Quantity = 100, IsAttach = attach,
            DeliveryOrder = new() { CompanyId = owner, Status = status, CreatedDate = Due }
        };
        store.Seed(nameof(db.DeliveryOrderDetails), Receipt("Pending", company), Receipt("InProgress", company), Receipt("Canceled", company),
            Receipt("Completed", Guid.NewGuid()), Receipt("Completed", company, true), Receipt("Completed", company));
        var rows = await DeliveryOtdReader.ReadAsync(db, new() { From = Due, To = Due }, company, Due, default);
        Assert.Equal(300, Assert.Single(rows).DeliveredQuantity);
        Assert.Equal(0, store.Saves);
        var charts = DeliveryOtdCalculator.Charts(rows, Due);
        Assert.Equal(DeliveryOtdCalculator.Summarize(rows, Due), Assert.Single(charts.Monthly).Summary);
    }

    [Fact]
    public async Task PromisedDate_UsesLatestActiveCompanyMfgDate_ForPeriodAndDueDate()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
        var company = Guid.NewGuid();
        var line = new MerchandiseOrderDetail
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), ExpectedQuantity = 100,
            DeliveryRequestDate = Due.AddMonths(-1), ExpectedDeliveryDate = Due.AddMonths(1),
            MerchandiseOrder = new() { CompanyId = company, OrderType = OrderType.Merchandise, CreateDate = Due }
        };
        store.Seed(nameof(db.MerchandiseOrderDetails), line);
        MfgOrderPO Link(DateTime? date, Guid owner, bool linkActive = true, bool mfgActive = true, string status = "New") => new()
        {
            MerchandiseOrderDetailId = line.MerchandiseOrderDetailId, Detail = line, IsActive = linkActive,
            ProductionOrder = new() { CompanyId = owner, IsActive = mfgActive, ExpectedDate = date, Status = status }
        };
        store.Seed(nameof(db.MfgOrderPOs), Link(Due.AddDays(-1), company), Link(Due, company), Link(null, company),
            Link(Due.AddMonths(1), Guid.NewGuid()), Link(Due.AddMonths(1), company, linkActive: false),
            Link(Due.AddMonths(1), company, mfgActive: false), Link(Due.AddMonths(1), company, status: "Canceled"),
            Link(Due.AddMonths(1), company, status: "Cancelled"));
        var rows = await DeliveryOtdReader.ReadAsync(db,
            new() { From = Due, To = Due, DateBasis = DeliveryOtdDateBasis.Promised }, company, Due, default);
        var row = Assert.Single(rows);
        Assert.Equal(Due, row.PromisedDate);
        Assert.Equal(Due, row.DueDate);
        Assert.Empty(await DeliveryOtdReader.ReadAsync(db,
            new() { From = Due.AddDays(-1), To = Due.AddDays(-1), DateBasis = DeliveryOtdDateBasis.Promised }, company, Due, default));
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData("DispatchUser", true)]
    [InlineData("PLPUUser", true)]
    [InlineData("HRUser", false)]
    public void Access_UsesDispatchReaders_AndRequiresAuthenticatedCompany(string role, bool allowed)
    {
        var user = new User(role);
        var filter = new DeliveryOtdFilter { From = Due, To = Due };
        Assert.Equal(allowed, DeliveryOtdReader.IsValid(filter, user, Due));
        Assert.False(DeliveryOtdReader.IsValid(filter, user with { IsAuthenticated = false }, Due));
        Assert.False(DeliveryOtdReader.IsValid(filter, user with { CompanyId = null }, Due));
    }

    [Fact]
    public void ImsOtdRequiresItsOwnPermissionAndEmployeeContext()
    {
        var user = new User(ApplicationRoles.InternalSystems.IMSUser)
        {
            EmployeeId = Guid.NewGuid(),
            AdditionalRole = ApplicationRoles.Accounting.ACCUser,
            HasExplicitPermissionSet = true,
        };
        var filter = new DeliveryOtdFilter { From = Due, To = Due };
        Assert.False(DeliveryOtdReader.IsValid(filter, user, Due, new CurrentUserPermissionService(user)));

        var allowed = user with { Permissions = [ApplicationPermissions.Reports.ViewDeliveryOtd] };
        Assert.True(DeliveryOtdReader.IsValid(filter, allowed, Due, new CurrentUserPermissionService(allowed)));
        var missingEmployee = allowed with { EmployeeId = null };
        Assert.False(DeliveryOtdReader.IsValid(filter, missingEmployee, Due, new CurrentUserPermissionService(missingEmployee)));
    }

    [Fact]
    public async Task MissingPromise_IsDiagnosticOnly_AndHeaderMatchesDetails()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
        var user = new User("DispatchUser");
        var line = new MerchandiseOrderDetail
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), ExpectedQuantity = 100, DeliveryRequestDate = Due,
            ExpectedDeliveryDate = Due,
            MerchandiseOrder = new() { CompanyId = user.CompanyId!.Value, OrderType = OrderType.Merchandise, CreateDate = Due }
        };
        store.Seed(nameof(db.MerchandiseOrderDetails), line);
        var header = await new GetDeliveryOtdHeaderQueryHandler(db, user, new Clock()).Handle(
            new() { From = Due, To = Due, DateBasis = DeliveryOtdDateBasis.Promised }, default);
        var detail = await new GetDeliveryOtdDetailsQueryHandler(db, user, new Clock()).Handle(
            new() { From = Due, To = Due, DateBasis = DeliveryOtdDateBasis.Promised,
                Status = DeliveryOtdStatus.MissingDueDate, PageSize = 1 }, default);
        Assert.True(header.Success);
        Assert.True(detail.Success);
        Assert.Equal(header.Data!.Summary.TotalLines, detail.Data!.TotalCount);
        Assert.Equal(1, header.Data.Summary.MissingDueDateLines);
        Assert.Equal(0, header.Data.Summary.DueLines);
        Assert.Null(header.Data.Summary.OtdPercent);
        Assert.Null(Assert.Single(detail.Data.Items).DueDate);
        var charts = await new HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdCharts.GetDeliveryOtdChartsQueryHandler(db, user, new Clock())
            .Handle(new() { From = Due, To = Due, DateBasis = DeliveryOtdDateBasis.Promised }, default);
        Assert.True(charts.Success);
        Assert.Equal(header.Data.PoSummary, charts.Data!.Monthly.Single(x => x.Key == "2026-10").PoSummary);
        Assert.All(charts.Data.Monthly.Where(x => x.Key != "2026-10"), x => Assert.Null(x.PoSummary!.PoOtdPercent));
    }

    [Fact]
    public async Task Handlers_RejectUnauthorizedAndInvalidPagingBeforeReadingDatabase()
    {
        var filter = new GetDeliveryOtdHeaderQuery { From = Due, To = Due };
        var denied = await new GetDeliveryOtdHeaderQueryHandler(null!, new User("HRUser"), new Clock())
            .Handle(filter, default);
        Assert.False(denied.Success);
        var paging = await new GetDeliveryOtdDetailsQueryHandler(null!, new User("DispatchUser"), new Clock())
            .Handle(new() { From = Due, To = Due, Page = int.MaxValue, PageSize = 200 }, default);
        Assert.False(paging.Success);
        Assert.False(DeliveryOtdReader.IsValid(new() { From = Due, To = Due.AddDays(-1) }, new User("DispatchUser"), Due));
        Assert.False(DeliveryOtdReader.IsValid(new() { From = Due, To = Due, AsOf = Due.AddDays(1) }, new User("DispatchUser"), Due));
    }

    [Fact]
    public async Task OrderCreationCutoff_IsInclusive_AndAppliesToLinesAndPos()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
        var company = Guid.NewGuid();
        MerchandiseOrderDetail Detail(DateTime created) => new()
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), ExpectedQuantity = 100, DeliveryRequestDate = Due,
            MerchandiseOrder = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = company,
                OrderType = OrderType.Merchandise, CreateDate = created }
        };
        var old = Detail(new(2026, 2, 28, 23, 59, 59));
        var boundary = Detail(new(2026, 3, 1));
        var later = Detail(Due);
        store.Seed(nameof(db.MerchandiseOrderDetails), old, boundary, later);
        store.Seed(nameof(db.MerchandiseOrders), old.MerchandiseOrder, boundary.MerchandiseOrder, later.MerchandiseOrder);
        var rows = await DeliveryOtdReader.ReadAsync(db, new() { From = Due, To = Due }, company, Due, default);
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, x => x.DetailId == old.MerchandiseOrderDetailId);
        var all = new[] { old, boundary, later }.Select(x => Line(Due) with
            { OrderId = x.MerchandiseOrder.MerchandiseOrderId }).ToList();
        var pos = await DeliveryOtdReader.ReadPoSummaryAsync(db, all, company, Due, default);
        Assert.Equal(2, pos.TotalPos);
    }

    private sealed class Clock : IDateTimeProvider
    {
        public DateTime Now => Due.AddDays(1);
    }

    private sealed record User(string Role) : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId { get; init; }
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public string? AdditionalRole { get; init; }
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public IReadOnlyCollection<string> Roles => AdditionalRole is { } extra ? [Role, extra] : [Role];
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
