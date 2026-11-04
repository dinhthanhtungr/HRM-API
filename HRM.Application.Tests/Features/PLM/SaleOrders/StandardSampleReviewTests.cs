using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class StandardSampleReviewTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0);

    [Theory]
    [InlineData("2024-10-25", "Approved", "New", true)]
    [InlineData("2024-10-26", "Approved", "New", false)]
    [InlineData("2024-10-27", "Approved", "New", false)]
    [InlineData("2026-10-25", "New", "New", true)]
    [InlineData("2026-10-25", "Cancelled", "New", true)]
    [InlineData("2026-10-25", "Approved", "WaitingForStandardSample", true)]
    [InlineData("2026-10-25", "Approved", "Cancelled", true)]
    public async Task Review_UsesEligibleHistory_AndDoesNotWrite(string date, string status, string detailStatus, bool required)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ISaleOrderDbContext>();
        var company = Guid.NewGuid();
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        var product = new HRM.Domain.Entities.SampleRequestSchema.Product
            { ProductId = Guid.NewGuid(), CompanyId = company, IsActive = true, ColourCode = "COLOR", Name = "Product" };
        store.Seed(nameof(db.Customers), customer);
        store.Seed(nameof(db.Products), product);
        store.Seed(nameof(db.MerchandiseOrders), new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CustomerId = customer.CustomerId, CompanyId = company, IsActive = true,
            Status = status, CreateDate = DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
            MerchandiseOrderDetails = [new() { ProductId = product.ProductId, IsActive = true, Status = detailStatus }]
        });
        var visibility = DispatchProxy.Create<ICustomerVisibilityService, Visibility>();
        ((Visibility)(object)visibility).Company = company;
        var result = await new SaleOrderStandardSampleService(db, visibility).ReviewAsync(new()
        {
            CustomerId = customer.CustomerId,
            SaleOrderDetails = [new() { ProductId = product.ProductId, FormulaId = Guid.NewGuid(), ExpectedQuantity = 1 }]
        }, company, Now, default);
        Assert.True(result.Success);
        Assert.Equal(required ? 1 : 0, result.Data!.Count);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task Review_RejectsCustomerOutsideCompany_BeforeReturningProductData()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ISaleOrderDbContext>();
        var company = Guid.NewGuid();
        var foreign = new Customer { CustomerId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        store.Seed(nameof(db.Customers), foreign);
        var visibility = DispatchProxy.Create<ICustomerVisibilityService, Visibility>();
        ((Visibility)(object)visibility).Company = company;
        var result = await new SaleOrderStandardSampleService(db, visibility)
            .ReviewAsync(new() { CustomerId = foreign.CustomerId }, company, Now, default);
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public void WaitingMfg_HasNoEditorOrPlanningActions()
    {
        var status = nameof(ManufacturingProductOrder.WaitingForStandardSample);
        Assert.Empty(ManufacturingEditorPolicy.Actions(status, true, true, true, true));
        foreach (var action in Enum.GetValues<ManufacturingInformationAction>())
            Assert.False(ManufacturingInformationActionRules.CanApply(status, action));
        Assert.Contains("Save", ManufacturingEditorPolicy.Actions("New", true, true, true));
    }

    public class Visibility : DispatchProxy
    {
        public Guid Company { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
        {
            "BuildScopeAsync" => Task.FromResult(new ViewerScope(Company, Guid.NewGuid(), true, true,
                new HashSet<Guid>(), new HashSet<Guid>(), Now)),
            "ApplyCustomerVisibility" => ((IQueryable<Customer>)args![0]!).Where(x => x.CompanyId == Company),
            _ => throw new NotSupportedException(targetMethod.Name)
        };
    }
}
