using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.SaleOrders.Commands.UpdateSaleOrder;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrderById;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderHeaderPatchTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime OriginalDate = new(2026, 11, 1);

    [Theory]
    [InlineData("{\"receiver\":\"New receiver\",\"phoneSnapshot\":\"0900000000\",\"deliveryAddress\":\"New address\"}", 8, false)]
    [InlineData("{\"vat\":null,\"paymentDate\":null}", 8, false)]
    [InlineData("{\"vat\":0}", 0, false)]
    [InlineData("{\"vat\":10}", 10, false)]
    [InlineData("{\"clearFields\":[\"vat\"]}", null, false)]
    [InlineData("{\"clearFields\":[\"paymentDate\"]}", 8, true)]
    [InlineData("{\"clearFields\":[\"vat\",\"paymentDate\"]}", null, true)]
    [InlineData("{\"vat\":null,\"clearFields\":[\"vat\",\"vat\"]}", null, false)]
    public async Task Patch_PreservesOmittedFields_AndClearsOnlyExplicitFields(string json, int? vat, bool clearDate)
    {
        var (db, store, order, user) = Setup();
        var request = JsonSerializer.Deserialize<UpdateSaleOrderRequest>(json, Json)! with { MerchandiseOrderId = order.MerchandiseOrderId };
        var result = await new UpdateSaleOrderCommandHandler(db, user, new Clock()).Handle(new(request), default);
        Assert.True(result.Success);
        Assert.Equal(vat.HasValue ? (decimal?)vat.Value : null, order.Vat);
        Assert.Equal(clearDate ? null : (DateTime?)OriginalDate, order.PaymentDate);
        Assert.Equal(1000m, order.TotalPrice);
        Assert.Equal("Transfer", order.PaymentType);
        Assert.True(order.IsPaid);
        Assert.Equal("Approved", order.Status);
        Assert.Equal(1, store.Saves);
        Assert.True(store.Transaction.Committed);
        var detail = await new GetSaleOrderByIdQueryHandler(db, user, new Clock())
            .Handle(new(order.MerchandiseOrderId), default);
        Assert.NotNull(detail);
        Assert.Equal(order.Vat, detail.Vat);
        Assert.Equal(order.PaymentDate, detail.PaymentDate);
        Assert.Equal(order.TotalPrice, detail.TotalPrice);
        var changed = vat != 8 || clearDate;
        Assert.Equal(changed ? 1 : 0, store.Added.OfType<EventLog>().Count());
        if (changed)
        {
            var audit = Assert.Single(store.Added.OfType<EventLog>());
            Assert.Equal(order.CompanyId, audit.CompanyId);
            Assert.Equal(user.EmployeeId, audit.EmployeeID);
            Assert.Contains("oldValue", audit.Note);
            Assert.Contains("newValue", audit.Note);
        }
    }

    [Fact]
    public async Task Patch_UpdatesPaymentDate_WithoutTouchingVat()
    {
        var (db, _, order, user) = Setup();
        var next = OriginalDate.AddDays(1);
        var result = await new UpdateSaleOrderCommandHandler(db, user, new Clock()).Handle(new(new()
        { MerchandiseOrderId = order.MerchandiseOrderId, PaymentDate = next }), default);
        Assert.True(result.Success);
        Assert.Equal(next, order.PaymentDate);
        Assert.Equal(8m, order.Vat);
    }

    [Theory]
    [InlineData("{\"vat\":0,\"clearFields\":[\"vat\"]}")]
    [InlineData("{\"paymentDate\":\"2026-11-02\",\"clearFields\":[\"paymentDate\"]}")]
    [InlineData("{\"clearFields\":[\"totalPrice\"]}")]
    [InlineData("{\"clearFields\":[null]}")]
    public async Task InvalidClear_IsRejectedBeforeAnyMutation(string json)
    {
        var (db, store, order, user) = Setup();
        var request = JsonSerializer.Deserialize<UpdateSaleOrderRequest>(json, Json)! with { MerchandiseOrderId = order.MerchandiseOrderId };
        var result = await new UpdateSaleOrderCommandHandler(db, user, new Clock()).Handle(new(request), default);
        Assert.False(result.Success);
        Assert.Equal(8m, order.Vat);
        Assert.Equal(OriginalDate, order.PaymentDate);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Patch_RejectsForeignCompanyOrInactiveOrder(bool foreignCompany)
    {
        var (db, store, order, user) = Setup();
        if (foreignCompany) order.CompanyId = Guid.NewGuid();
        else order.IsActive = false;
        var result = await new UpdateSaleOrderCommandHandler(db, user, new Clock()).Handle(new(new()
        { MerchandiseOrderId = order.MerchandiseOrderId, ClearFields = ["vat", "paymentDate"] }), default);
        Assert.False(result.Success);
        Assert.Equal(8m, order.Vat);
        Assert.Equal(OriginalDate, order.PaymentDate);
        Assert.Equal(0, store.Saves);
    }

    private static (ISaleOrderDbContext, ProductionOrderTestStore, MerchandiseOrder, User) Setup()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<ISaleOrderDbContext>();
        var user = new User();
        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true,
            Vat = 8m, PaymentDate = OriginalDate, TotalPrice = 1000m, PaymentType = "Transfer",
            IsPaid = true, Status = "Approved", ExternalId = "TEST-PATCH"
        };
        store.Seed(nameof(db.MerchandiseOrders), order);
        return (db, store, order, user);
    }

    private sealed class Clock : IDateTimeProvider { public DateTime Now => new(2026, 10, 29); }
    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
