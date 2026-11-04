using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Tests.Features.PLM.ProductionOrders;

public sealed partial class ProductionOrderCreationTests
{
    private sealed class Fixture
    {
        public static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0);
        public TestUser User { get; } = new();
        public ProductionOrderTestStore Store { get; }
        public TestTimeline Timeline { get; } = new();
        public Product Product { get; }
        public Material Material { get; }
        public Category Category { get; }
        public MerchandiseOrder SaleOrder { get; }
        public MerchandiseOrderDetail Detail { get; }
        public CreateProductionOrderInformCommandHandler Inform { get; }
        public CreateInternalProductionOrderCommandHandler Internal { get; }

        public Fixture()
        {
            var (db, store) = ProductionOrderTestStore.Create();
            Store = store;
            Product = new() { ProductId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true, Name = "DB-PRODUCT", ColourCode = "P001" };
            Material = new() { MaterialId = Guid.NewGuid(), CompanyId = User.CompanyId.Value, IsActive = true };
            Category = new() { CategoryId = Guid.NewGuid(), CompanyId = User.CompanyId.Value, IsActive = true };
            SaleOrder = new() { MerchandiseOrderId = Guid.NewGuid(), CompanyId = User.CompanyId.Value, IsActive = true, Status = "Approved" };
            Detail = new() { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = SaleOrder.MerchandiseOrderId, MerchandiseOrder = SaleOrder, ProductId = Product.ProductId, IsActive = true };
            Store.Seed(nameof(db.Products), Product);
            Store.Seed(nameof(db.Materials), Material);
            Store.Seed(nameof(db.Categories), Category);
            Store.Seed(nameof(db.MerchandiseOrders), SaleOrder);
            Store.Seed(nameof(db.MerchandiseOrderDetails), Detail);
            var permissions = new CurrentUserPermissionService(User);
            var codes = new TestCodes();
            var clock = new TestClock();
            Inform = new(db, User, permissions, codes, clock, Timeline,
                new ProductionOrderReferenceValidator(db), new ProductionOrderReservationService(db));
            Internal = new(db, User, permissions, codes, clock, Timeline);
        }

        public CreateProductionOrderInformRequest Request() => new()
        {
            MerchandiseOrderId = SaleOrder.MerchandiseOrderId, MerchandiseOrderDetailId = Detail.MerchandiseOrderDetailId,
            ProductId = Product.ProductId, ProductNameSnapshot = "REQUEST-PRODUCT", RequiredDate = Now,
            ExpectedDate = Now.AddDays(2), TotalQuantityRequest = 100, TotalQuantity = 100,
            LabNote = "LAB", PlpuNote = "PLPU"
        };

        public CreateProductionOrderInformRequest RequestWithFormula()
        {
            var request = Request();
            request.FormulaItems =
            [
                new() { ItemId = Product.ProductId, ItemType = ItemType.ProductFailure, CategoryId = Category.CategoryId,
                    Quantity = 0.4m, UnitPrice = 20, LineNo = 8, MaterialExternalIdSnapshot = "WRONG-PRODUCT-SNAPSHOT",
                    LotNumber = new() { LotNo = " n/a ", StockType = StockType.FinishedGood } },
                new() { ItemId = Material.MaterialId, ItemType = ItemType.Material, CategoryId = Category.CategoryId,
                    Quantity = 0.6m, UnitPrice = 10, LineNo = 3, MaterialExternalIdSnapshot = " m001 ",
                    LotNumber = new() { LotNo = " LOT-1 ", StockType = StockType.DefectiveRawMaterial } }
            ];
            return request;
        }
    }

    internal sealed class TestUser : ICurrentUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; set; } = Guid.NewGuid();
        public Guid? CompanyId { get; set; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; set; } = [ApplicationRoles.Production.PLPUUser];
        public IReadOnlyCollection<string> Permissions { get; set; } = [];
        public bool HasExplicitPermissionSet { get; set; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TestClock : IDateTimeProvider { public DateTime Now => Fixture.Now; }
    private sealed class TestCodes : IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Creation must use monthly codes");
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken cancellationToken = default)
            => Task.FromResult(prefix + "261000001");
    }
    private sealed class TestTimeline : IEventLogWriter
    {
        public List<EventLogCreateRequest> Logs { get; } = [];
        public Task AddAsync(EventLogCreateRequest request, CancellationToken cancellationToken = default)
        { Logs.Add(request); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken cancellationToken = default)
        { Logs.AddRange(requests); return Task.CompletedTask; }
    }
}
