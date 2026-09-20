using System.Text.Json;
using HRM.Application.Features.PLM.Materials.Dtos.Preview;
using HRM.Application.Features.PLM.Materials.Rules;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class MaterialPurchaseAvailabilityRulesTests
{
    [Fact]
    public void Missing_status_defaults_to_available()
    {
        Assert.Equal(
            MaterialPurchaseStatus.Available,
            MaterialPurchaseAvailabilityRules.ResolveStatus(null));
        Assert.True(MaterialPurchaseAvailabilityRules.IsPurchaseAvailable(null));
    }

    [Theory]
    [InlineData(MaterialPurchaseStatus.Available, true)]
    [InlineData(MaterialPurchaseStatus.Unavailable, false)]
    public void Availability_is_derived_from_status(
        MaterialPurchaseStatus status,
        bool expected)
        => Assert.Equal(
            expected,
            MaterialPurchaseAvailabilityRules.IsPurchaseAvailable(status));

    [Fact]
    public void Material_preview_serializes_the_shared_nested_contract()
    {
        var dto = new MaterialPreviewDto
        {
            PurchaseAvailability = MaterialPurchaseAvailabilityRules.Resolve(
                MaterialPurchaseStatus.Unavailable,
                "Supplier stopped production",
                new DateTime(2026, 9, 15),
                null)
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var availability = root.GetProperty("purchaseAvailability");

        Assert.Equal("Unavailable", availability.GetProperty("status").GetString());
        Assert.False(availability.GetProperty("isPurchaseAvailable").GetBoolean());
        Assert.False(root.TryGetProperty("purchaseStatus", out _));
        Assert.False(root.TryGetProperty("isPurchaseAvailable", out _));
    }
}
