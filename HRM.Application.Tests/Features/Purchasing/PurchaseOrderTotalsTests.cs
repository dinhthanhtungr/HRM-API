using ClosedXML.Excel;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderPdf;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.Documents.Excels;
using HRM.Infrastructure.Documents.Pdfs;
using Xunit;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderTotalsTests
{
    [Theory]
    [InlineData(false, false, null)]
    [InlineData(false, true, null)]
    [InlineData(false, true, 0)]
    [InlineData(false, true, 77760000)]
    [InlineData(true, true, null)]
    [InlineData(true, true, 0)]
    public async Task DetailListMapperAndDocumentsAgree(bool inactive, bool hasSnapshot, int? stored)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = user.CompanyId, IsActive = !inactive,
            PurchaseOrderSnapshot = hasSnapshot ? new() { Vat = 8, TotalPrice = stored } : null,
            PurchaseOrderDetails = [new() { IsActive = !inactive, TotalPriceAgreed = 36000000m },
                new() { IsActive = !inactive, TotalPriceAgreed = 36000000m }]
        };
        foreach (var line in po.PurchaseOrderDetails) line.PurchaseOrderId = po.PurchaseOrderId;
        store.Seed(nameof(db.PurchaseOrders), po);
        var reader = new PurchaseOrderReceiptReader(db);
        var handler = new GetPurchaseOrderByIdQueryHandler(db, user, reader);
        var detail = handler.BuildQuery(po.PurchaseOrderId, user.CompanyId!.Value, inactive).Single();
        var list = await new GetPurchaseOrdersQueryHandler(db, user, reader).Handle(new() { IncludeInactive = inactive }, default);
        var mapped = PurchaseOrderDtoMapper.Map(po, "", po.PurchaseOrderSnapshot, detail.Items);
        var expected = stored ?? (hasSnapshot ? 77760000m : 72000000m);
        foreach (var dto in new PurchaseOrderListItemDto[] { detail, Assert.Single(list.Items), mapped })
        {
            Assert.Equal(72000000m, dto.Subtotal);
            Assert.Equal(hasSnapshot ? 5760000m : 0m, dto.VatAmount);
            Assert.Equal(expected, dto.TotalPrice);
            Assert.Equal(stored == 0, dto.HasTotalPriceMismatch);
        }
        Assert.Empty(handler.BuildQuery(po.PurchaseOrderId, Guid.NewGuid(), true));
        if (inactive) Assert.Empty(handler.BuildQuery(po.PurchaseOrderId, user.CompanyId.Value));
        Assert.Equal(0, store.Saves);
        using var workbook = new XLWorkbook(new MemoryStream(new PurchaseOrderExcelRenderer().Render(detail)));
        Assert.Equal(expected, workbook.Worksheet("Purchase Order").Cell(10, 8).GetValue<decimal>());
        if (!inactive)
        {
            var pdf = new ExportPurchaseOrderPdfQueryHandler(db, null!, null!, null!, null!).BuildQuery(po.PurchaseOrderId, user.CompanyId.Value).Single();
            Assert.Equal(expected, PurchaseOrderPdfRenderer.CalculateTotals(pdf).GrandTotal);
        }
    }

    [Theory]
    [InlineData(72000000, 8, 77760000)]
    [InlineData(72000000, 0, 72000000)]
    [InlineData(0, 8, 0)]
    [InlineData(1.23445, 8, 1.33)]
    [InlineData(1.005, 0, 1.01)]
    public void MoneyRoundingMatchesSnapshotPrecision(decimal subtotal, int vat, decimal expected)
    {
        Assert.Equal(expected, PurchaseOrderTotals.Calculate(subtotal, vat));
    }

    [Fact]
    public void StoredZeroAndZeroLinesAreValid()
    {
        var dto = new PurchaseOrderDetailDto { StoredTotalPrice = 0m, Subtotal = 0m, Vat = 8 };
        Assert.Equal(0m, dto.TotalPrice);
        Assert.False(dto.HasTotalPriceMismatch);
    }

    [Theory]
    [InlineData(false, 10.25, 11.07)]
    [InlineData(true, 30.75, 33.21)]
    public void DetailSubtotalRespectsLineScopeAndNullAmounts(bool includeInactive, decimal subtotal, decimal total)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var company = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            PurchaseOrderSnapshot = new() { Vat = 8 },
            PurchaseOrderDetails = [new() { IsActive = true, TotalPriceAgreed = 10.25m },
                new() { IsActive = false, TotalPriceAgreed = 20.5m },
                new() { IsActive = true, TotalPriceAgreed = null, RequestQuantity = 100m, UnitPriceAgreed = 999m }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        var dto = new GetPurchaseOrderByIdQueryHandler(db, null!, null!).BuildQuery(po.PurchaseOrderId, company, includeInactive).Single();
        Assert.Equal(subtotal, dto.Subtotal);
        Assert.Equal(total, dto.TotalPrice);
    }

    [Theory]
    [InlineData(8, 50, 720000, 77760000)]
    [InlineData(0, 50, 720000, 72000000)]
    [InlineData(8, 50, 0, 0)]
    [InlineData(8, 1.25, 1.234567, 3.33)]
    public async Task CreatePersistsServerCalculatedTotal(int vat, decimal quantity, decimal price, decimal expected)
    {
        var db = DispatchProxy.Create<IPurchaseOrderDbContext, CreateStore>();
        var store = (CreateStore)db;
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var supplier = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = user.CompanyId, IsActive = true };
        var materials = new[] { new Material { MaterialId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true },
            new Material { MaterialId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, IsActive = true } };
        store.Seed(nameof(db.Suppliers), supplier);
        store.Seed(nameof(db.Materials), materials);
        store.Seed(nameof(db.Employees), new HRM.Domain.Entities.HrSchema.Employee { EmployeeId = user.EmployeeId!.Value, CompanyId = user.CompanyId });
        var handler = new CreatePurchaseOrderCommandHandler(db, user, new CurrentUserPermissionService(user), new Ids(), new Clock());
        var result = await handler.Handle(new(new CreatePurchaseOrderRequest
        {
            SupplierId = supplier.SupplierId, Vat = vat, OrderType = "NCC", RequestDeliveryDate = new(2026, 10, 1),
            RealDeliveryDate = new(2099, 1, 1),
            Items = materials.Select(x => new PurchaseOrderLineRequest { MaterialId = x.MaterialId, Quantity = quantity, UnitPriceAgreed = price }).ToArray()
        }), default);
        Assert.True(result.Success, result.Message);
        Assert.Null(Assert.Single(store.Added.OfType<PurchaseOrder>()).RealDeliveryDate);
        Assert.Null(result.Data!.RealDeliveryDate);
        Assert.Equal(expected, Assert.Single(store.Added.OfType<PurchaseOrderSnapshot>()).TotalPrice);
        Assert.Equal(expected, result.Data!.TotalPrice);
        Assert.False(result.Data.HasTotalPriceMismatch);
        Assert.True(store.Transaction.Committed);
    }

    private sealed class Ids : IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken ct = default) => Task.FromResult("DDH-TEST");
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken ct = default) => Task.FromResult("DDH-TEST");
    }
    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 10, 16); }

    public class CreateStore : PurchaseOrderParityTests.PurchaseTestStore
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_PurchaseOrderDetails" => new WriteSet<PurchaseOrderDetail>(Added),
            "get_PurchaseOrderLinks" => new WriteSet<PurchaseOrderLink>(Added),
            _ => base.Invoke(method, args)
        };
    }

    private sealed class WriteSet<T>(List<object> added) : DbSet<T> where T : class
    {
        public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
        public override Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            added.AddRange(entities.Cast<object>());
            return Task.CompletedTask;
        }
    }
}
