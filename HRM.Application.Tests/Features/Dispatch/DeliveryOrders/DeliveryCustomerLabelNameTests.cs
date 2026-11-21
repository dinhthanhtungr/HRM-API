using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.PrintectSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryCustomerLabelNameTests
{
    [Fact]
    public void UsesNameForExactCustomerAndProduct_WithoutChangingProductOrSnapshot()
    {
        var f = new Fixture();
        var label = f.Label("  Tên riêng của khách hàng  "); label.FieldKey = " name ";
        Assert.Equal("Tên riêng của khách hàng", f.Name(label));
        Assert.Equal("Current product name", f.Product.Name);
        Assert.Equal("Snapshot name", f.Order.Details.Single().ProductNameSnapShot);
    }

    [Theory]
    [InlineData("blank")] [InlineData("null")] [InlineData("otherField")]
    [InlineData("inactiveDetail")] [InlineData("inactiveHeader")]
    [InlineData("otherCustomer")] [InlineData("otherProduct")]
    [InlineData("foreignCustomer")] [InlineData("foreignProduct")]
    public void InvalidLabelFallsBackToCurrentProduct(string kind)
    {
        var f = new Fixture(); var label = f.Label("Hidden name");
        if (kind == "blank") label.FieldValue = "  ";
        if (kind == "null") label.FieldValue = null;
        if (kind == "otherField") label.FieldKey = "Color";
        if (kind == "inactiveDetail") label.IsActive = false;
        if (kind == "inactiveHeader") label.Header.IsActive = false;
        if (kind == "otherCustomer") label.Header.CustomerId = Guid.NewGuid();
        if (kind == "otherProduct") label.Header.ProductId = Guid.NewGuid();
        if (kind == "foreignCustomer") label.Header.Customer = new Customer { CompanyId = Guid.NewGuid() };
        if (kind == "foreignProduct") label.Header.Product = new Product { CompanyId = Guid.NewGuid() };
        Assert.Equal("Current product name", f.Name(label));
    }

    [Fact]
    public void NewestActiveNonBlankNameWinsWithoutMultiplyingLinesOrQuantity()
    {
        var f = new Fixture(); var old = f.Label("Old name");
        var latest = f.Label("New name"); latest.Header.UpdatedDate = new(2026, 11, 9);
        Assert.Equal("New name", f.Name(old, latest));
        Assert.Equal("New name", f.Name(latest, old));
        var document = DeliveryOrderDocumentQuery.Project(new[] { f.Order }.AsQueryable(), f.Order.Id, f.Order.CompanyId,
            DeliveryCustomerLabelNames.Query(new[] { old, latest }.AsQueryable(), f.Order.CompanyId)).Single();
        Assert.Single(document.Lines);
        Assert.Equal(50m, document.TotalQuantity);
    }

    [Fact]
    public void ProjectionWithLabelsTranslatesOnPostgresAndDoesNotReadCost()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var sql = DeliveryOrderDocumentQuery.Project(db.DeliveryOrders.AsNoTracking(), Guid.NewGuid(), company,
            DeliveryCustomerLabelNames.Query(db.CustomerLabelDetails.AsNoTracking(), company)).ToQueryString();
        Assert.Contains("CustomerLabelDetails", sql);
        Assert.Contains("CustomerLabelHeaders", sql);
        Assert.DoesNotContain("UnitCostSnapshot", sql);
        Assert.DoesNotContain("TotalCostSnapshot", sql);
        Assert.DoesNotContain("DeliveryPrice", sql);
    }

    [Fact]
    public async Task DetailListAndSelectableLinesUseTheSameCustomerLabelName()
    {
        var f = new Fixture();
        var (db, store) = HRM.Application.Tests.Features.DevAndQA.IQC.IqcTestStore
            .CreateContext<HRM.Application.Abstractions.Persistence.Dispatch.IDispatchReadDbContext>();
        var user = new HRM.Application.Tests.Features.DevAndQA.IQC.IqcWorkflowTests.User
            { Roles = ["Developer"], CompanyId = f.Order.CompanyId };
        var permissions = new HRM.Application.Commons.Authorization.CurrentUserPermissionService(user);
        store.Seed(nameof(db.DeliveryOrders), f.Order);
        store.Seed(nameof(db.CustomerLabelDetails), f.Label("Customer label name"));
        var detail = await new HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrderDetail.GetDeliveryOrderDetailQueryHandler(db, user, permissions)
            .Handle(new(f.Order.Id), default);
        Assert.Equal("Customer label name", Assert.Single(detail!.Lines).ProductDisplayName);
        var list = await new HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrders.GetDeliveryOrdersQueryHandler(db, user, permissions)
            .Handle(new(), default);
        Assert.Equal("Customer label name", Assert.Single(Assert.Single(list.Items).Lines).ProductDisplayName);

        var merchandise = new HRM.Domain.Entities.OrderSchema.MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CompanyId = f.Order.CompanyId, CustomerId = f.Order.CustomerId,
            Customer = f.Order.Customer, IsActive = true,
        };
        store.Seed(nameof(db.MerchandiseOrderDetails), new HRM.Domain.Entities.OrderSchema.MerchandiseOrderDetail
        {
            MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = merchandise.MerchandiseOrderId,
            MerchandiseOrder = merchandise, ProductId = f.Product.ProductId, Product = f.Product,
            ProductExternalIdSnapshot = "SP1", ExpectedQuantity = 100, IsActive = true,
        });
        var selectable = await new HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines.GetSelectableDeliveryLinesQueryHandler(
            db, user, new HRM.Application.Features.Dispatch.DeliveryOrders.DeliveryOrderLotInventoryService(db)).Handle(new(), default);
        Assert.Equal("Customer label name", Assert.Single(Assert.Single(selectable.Items).Lines).ProductDisplayName);
        Assert.Equal(0, store.Saves);
    }

    private sealed class Fixture
    {
        internal readonly DeliveryOrder Order;
        internal readonly Product Product;
        internal Fixture()
        {
            var company = Guid.NewGuid();
            Product = new() { ProductId = Guid.NewGuid(), CompanyId = company, Name = "Current product name", ColourCode = "SP1" };
            Order = new() { Id = Guid.NewGuid(), CompanyId = company, CustomerId = Guid.NewGuid(),
                Company = new Company { Name = "Company" }, Customer = new Customer { CompanyId = company, CustomerName = "Customer" } };
            Order.Details.Add(new() { ProductId = Product.ProductId, Product = Product, ProductNameSnapShot = "Snapshot name", Quantity = 50 });
        }
        internal CustomerLabelDetail Label(string name) => new()
        {
            Id = Guid.NewGuid(), FieldKey = "Name", FieldValue = name,
            Header = new CustomerLabelHeader { Id = Guid.NewGuid(), CustomerId = Order.CustomerId, ProductId = Product.ProductId,
                Customer = Order.Customer, Product = Product, CreatedDate = new(2026, 11, 1) },
        };
        internal string? Name(params CustomerLabelDetail[] labels) =>
            DeliveryOrderDocumentQuery.Project(new[] { Order }.AsQueryable(), Order.Id, Order.CompanyId,
                DeliveryCustomerLabelNames.Query(labels.AsQueryable(), Order.CompanyId)).Single().Lines.Single().ProductName;
    }
}
