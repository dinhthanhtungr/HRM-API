using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.FillManufacturingColourNames;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingColourNameFillTests
{
    [Theory]
    [InlineData("unauthenticated")]
    [InlineData("company")]
    [InlineData("employee")]
    [InlineData("view")]
    [InlineData("technical")]
    public async Task InvalidAccessStopsBeforeReadingDatabase(string denied)
    {
        var user = new ProductionOrderCreationTests.TestUser { Roles = [ApplicationRoles.Lab.LabUser] };
        if (denied == "unauthenticated") user.IsAuthenticated = false;
        if (denied == "company") user.CompanyId = Guid.Empty;
        if (denied == "employee") user.EmployeeId = null;
        if (denied == "view") user.HasExplicitPermissionSet = true;
        if (denied == "technical") user.Roles = [ApplicationRoles.Production.ManufactureUser];
        var handler = new FillManufacturingColourNamesCommandHandler(null!, user, new CurrentUserPermissionService(user), new Clock(), new Log());
        Assert.False((await handler.Handle(new(), default)).Success);
    }

    [Fact]
    public async Task PreviewOnlyExaminesNullActiveCompanySnapshotsAndLimitsItems()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IManufacturingDbContext>();
        var user = new ProductionOrderCreationTests.TestUser { Roles = [ApplicationRoles.Lab.LabUser] };
        var company = user.CompanyId!.Value;
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company, IsActive = true, ColourName = "Red" };
        var inactive = new Product { ProductId = Guid.NewGuid(), CompanyId = company, IsActive = false, ColourName = "Blue" };
        var foreign = new Product { ProductId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true, ColourName = "Blue" };
        var blank = new Product { ProductId = Guid.NewGuid(), CompanyId = company, IsActive = true, ColourName = " " };
        store.Seed(nameof(db.Products), product, inactive, foreign, blank);
        var orders = Enumerable.Range(0, 251).Select(_ => Order(product.ProductId)).ToList();
        orders.AddRange(new[] { Order(inactive.ProductId), Order(foreign.ProductId), Order(blank.ProductId), Order(Guid.NewGuid()) });
        orders.Add(Order(product.ProductId, "")); orders.Add(Order(product.ProductId, " ")); orders.Add(Order(product.ProductId, "Old"));
        var off = Order(product.ProductId); off.IsActive = false; orders.Add(off);
        var other = Order(product.ProductId); other.CompanyId = Guid.NewGuid(); orders.Add(other);
        store.Seed(nameof(db.MfgProductionOrders), orders.ToArray());
        var log = new Log();
        var result = await new FillManufacturingColourNamesCommandHandler(db, user, new CurrentUserPermissionService(user), new Clock(), log).Handle(new(), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(255, result.Data!.Examined); Assert.Equal(251, result.Data.Matched);
        Assert.Equal(0, result.Data.Updated); Assert.Equal(100, result.Data.Items.Count); Assert.True(result.Data.HasMoreItems);
        Assert.Equal(0, store.Saves); Assert.Empty(log.Items);
        Assert.Equal(255, orders.Count(x => x.CompanyId == company && x.IsActive && x.ColorName is null));

        MfgProductionOrder Order(Guid productId, string? color = null) => new()
        { MfgProductionOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true, ProductId = productId, ColorName = color };
    }

    internal sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 11, 12, 10, 0, 0); }
    internal sealed class Log : IEventLogWriter
    {
        public List<EventLogCreateRequest> Items { get; } = [];
        public Func<Task>? AfterFirst { get; set; }
        public bool Throw { get; set; }
        public async Task AddAsync(EventLogCreateRequest request, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException("Simulated audit failure");
            Items.Add(request);
            if (Items.Count == 1 && AfterFirst is not null) await AfterFirst();
        }
        public async Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken cancellationToken = default)
        { foreach (var request in requests) await AddAsync(request, cancellationToken); }
    }
}
