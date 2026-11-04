using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Materials.Queries.GetSuppliers;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class SupplierSummaryAddressTests
{
    [Theory]
    [InlineData(true, "Primary full address")]
    [InlineData(false, "Secondary full address")]
    public async Task List_prefers_primary_and_uses_deterministic_active_fallback(bool hasPrimary, string expected)
    {
        var addresses = new List<SupplierAddress>
        {
            new() { AddressId = Guid.Parse("00000000-0000-0000-0000-000000000002"), IsActive = true, AddressLine = "Other address" },
            new() { AddressId = Guid.Parse("00000000-0000-0000-0000-000000000001"), IsActive = true, AddressLine = " Secondary full address " },
            new() { IsActive = false, IsPrimary = true, AddressLine = "Inactive address" },
            new() { IsActive = true, IsPrimary = true, AddressLine = "   " }
        };
        if (hasPrimary) addresses.Add(new() { IsActive = true, IsPrimary = true, AddressLine = "Primary full address" });
        var result = await ReadAsync(addresses, "Registration address");
        Assert.Equal(expected, Assert.Single(result.Items).SupplierAddress);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
    }

    [Theory]
    [InlineData(" Registered full address ", "Registered full address")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public async Task List_falls_back_to_registration_or_null(string? registration, string? expected)
    {
        var result = await ReadAsync([new() { IsActive = false, AddressLine = "Ignore" }], registration);
        Assert.Equal(expected, Assert.Single(result.Items).SupplierAddress);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var address = document.RootElement.GetProperty("items")[0].GetProperty("supplierAddress");
        Assert.Equal(expected, address.ValueKind == JsonValueKind.Null ? null : address.GetString());
    }

    private static async Task<HRM.Application.Commons.Pagination.PagedResult<HRM.Application.Features.PLM.Materials.Dtos.SupplierSummaryDto>> ReadAsync(
        List<SupplierAddress> addresses, string? registration)
    {
        var (db, store) = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var user = new IqcWorkflowTests.User();
        var supplier = new Supplier
        {
            SupplierId = Guid.NewGuid(), CompanyId = user.CompanyId, IsActive = true,
            SupplierName = "Supplier", SupplierAddresses = addresses, RegistrationAddress = registration
        };
        store.Seed(nameof(db.Suppliers), supplier,
            new Supplier { SupplierId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true },
            new Supplier { SupplierId = Guid.NewGuid(), CompanyId = user.CompanyId, IsActive = false });
        return await new GetSuppliersQueryHandler(db, user).Handle(new() { PageNumber = 1, PageSize = 20 }, default);
    }
}
