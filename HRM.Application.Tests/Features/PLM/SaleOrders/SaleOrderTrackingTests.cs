using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Merchadises;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class SaleOrderTrackingTests
{
    [Fact]
    public void TrackingLimitsCompanyAndHidesPauseReason()
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var company = Guid.NewGuid();
        var currentCompany = Row(company);
        store.Seed(nameof(db.MerchandiseOrders), currentCompany, Row(Guid.NewGuid()),
            Row(company, OrderType.Internal), Row(company, customerCode: "KH_VIETAUS"));
        var user = new User(company);
        var reader = new SaleOrderTrackingReader(db, user, new CurrentUserPermissionService(user));

        var record = Assert.Single(SaleOrderTrackingReader.Project(reader.Read()!));

        Assert.Equal(currentCompany.MerchandiseOrderId, record.OrderId);
        Assert.Equal("Paused", record.Status);
        var json = System.Text.Json.JsonSerializer.Serialize(record);
        Assert.DoesNotContain("PaymentHold", json);
        Assert.DoesNotContain("TotalPrice", json);
    }

    private static MerchandiseOrder Row(Guid company, OrderType type = OrderType.Merchandise, string customerCode = "CUSTOMER") => new()
    {
        MerchandiseOrderId = Guid.NewGuid(),
        ExternalId = "PO-001",
        CompanyId = company,
        Customer = new Customer { CompanyId = company, IsActive = true, ExternalId = customerCode },
        OrderType = type,
        IsDeliveryPaused = true,
        DeliveryPauseType = "PaymentHold",
        Status = nameof(MerchadiseStatus.Approved),
        TotalPrice = 1234567m,
    };

    private sealed class User(Guid companyId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = companyId;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = [ApplicationRoles.InternalSystems.IMSUser];
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
