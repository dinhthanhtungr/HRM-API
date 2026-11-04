using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Features.Reports.DeliveryOtd;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Tests.Features.Reports;

public sealed class DeliveryOtdPeriodDeliveryTests
{
    [Fact]
    public async Task PeriodQuantity_UsesDeliveryDatesScopeAndFilters_NotDueDates()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IDispatchReadDbContext>();
        var company = Guid.NewGuid(); var product = Guid.NewGuid();
        var customer = Guid.NewGuid(); var manager = Guid.NewGuid();
        var start = new DateTime(2026, 10, 1); var asOf = new DateTime(2026, 10, 15);
        DeliveryOrderDetail Row(DateTime date, decimal quantity = 10, string status = "Pending") => new()
        {
            Quantity = quantity, MerchandiseOrderDetailId = Guid.NewGuid(),
            DeliveryOrder = new() { CompanyId = company, CreatedDate = date, Status = status },
            MerchandiseOrderDetail = new()
            {
                ProductId = product, ExpectedQuantity = 1, DeliveryRequestDate = start.AddYears(-1),
                MerchandiseOrder = new() { CompanyId = company, CreateDate = new(2026, 3, 1),
                    OrderType = OrderType.Merchandise, CustomerId = customer, ManagerById = manager }
            }
        };
        var otherCompany = Row(start); otherCompany.DeliveryOrder.CompanyId = Guid.NewGuid();
        var otherOrderCompany = Row(start); otherOrderCompany.MerchandiseOrderDetail!.MerchandiseOrder.CompanyId = Guid.NewGuid();
        var oldOrder = Row(start); oldOrder.MerchandiseOrderDetail!.MerchandiseOrder.CreateDate = new(2026, 2, 28);
        var attach = Row(start); attach.IsAttach = true;
        var inactive = Row(start); inactive.IsActive = false;
        var otherProduct = Row(start); otherProduct.MerchandiseOrderDetail!.ProductId = Guid.NewGuid();
        store.Seed(nameof(db.DeliveryOrderDetails), Row(start), Row(asOf.AddHours(23), 20, "InProgress"),
            Row(start.AddDays(2), 30, "Completed"), Row(start.AddDays(-1)), Row(asOf.AddDays(1)),
            Row(start, status: "Canceled"), Row(start, status: " Cancelled "), otherCompany,
            otherOrderCompany, oldOrder, attach, inactive, otherProduct);
        var filter = new DeliveryOtdFilter { From = start, To = new(2026, 10, 31), ProductId = product,
            CustomerId = customer, ManagerId = manager, DateBasis = DeliveryOtdDateBasis.Promised };
        Assert.Equal(60, await DeliveryOtdPeriodDeliveryReader.ReadAsync(db, filter, company, asOf, default));
        Assert.Equal(10, await DeliveryOtdPeriodDeliveryReader.ReadAsync(db,
            new() { From = start, To = start, ProductId = product }, company, asOf, default));
        Assert.Equal(0, await DeliveryOtdPeriodDeliveryReader.ReadAsync(db,
            new() { From = start, To = asOf, CustomerId = Guid.NewGuid() }, company, asOf, default));
        Assert.Equal(0, await DeliveryOtdPeriodDeliveryReader.ReadAsync(db,
            new() { From = asOf.AddDays(1), To = asOf.AddDays(2) }, company, asOf, default));
        Assert.Equal(0, store.Saves);
    }
}
