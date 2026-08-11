using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using System.Text.Json;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderCostVisibilityRulesTests
{
    [Theory]
    [InlineData("ACUser")]
    [InlineData("PriceView")]
    [InlineData("SeePriceUser")]
    [InlineData("Admin")]
    public void AuthorizedFormulaPriceRole_CanViewCost(string role)
    {
        Assert.True(DeliveryOrderCostVisibilityRules.CanViewCost(new CurrentUser(role)));
    }

    [Theory]
    [InlineData("DispatchUser")]
    [InlineData("KHOUser")]
    [InlineData("SaleUser")]
    public void OperationalRole_CannotViewCost(string role)
    {
        Assert.False(DeliveryOrderCostVisibilityRules.CanViewCost(new CurrentUser(role)));
    }

    [Fact]
    public void UnauthorizedCost_NullFieldsAreOmittedFromJson()
    {
        var json = JsonSerializer.Serialize(new DeliveryOrderLotDto
        {
            LotNo = "LOT-A",
            Quantity = 1m,
            UnitCostSnapshot = null,
            TotalCostSnapshot = null
        });

        Assert.DoesNotContain("UnitCostSnapshot", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TotalCostSnapshot", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnauthorizedAvailableLotCost_NullFieldIsOmittedFromJson()
    {
        var json = JsonSerializer.Serialize(new AvailableDeliveryLotDto
        {
            LotNo = "LOT-A",
            OnHandQuantity = 10m,
            ReservedQuantity = 2m,
            AvailableQuantity = 8m,
            UnitCostSnapshot = null
        });

        Assert.DoesNotContain("UnitCostSnapshot", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LotRequest_DoesNotExposeCostFieldsForClientBinding()
    {
        var propertyNames = typeof(DeliveryOrderLotRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("UnitCostSnapshot", propertyNames);
        Assert.DoesNotContain("TotalCostSnapshot", propertyNames);
    }

    private sealed class CurrentUser(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId => Guid.NewGuid();
        public Guid? CompanyId => Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string requestedRole)
            => string.Equals(role, requestedRole, StringComparison.OrdinalIgnoreCase);
    }
}
