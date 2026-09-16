using HRM.Application.Features.PLM.Materials.Commands.CreateMaterialSupplier;
using HRM.Application.Features.PLM.Materials.Commands.PatchMaterialSupplier;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Services;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class MaterialSupplierManagementRulesTests
{
    [Fact]
    public void Create_requires_material_supplier_and_initial_price()
    {
        var request = new CreateMaterialSupplierRequest();

        Assert.Equal("MaterialId is required.", CreateMaterialSupplierCommandHandler.ValidateRequest(request));
    }

    [Fact]
    public void Create_accepts_zero_price_and_normalizes_currency()
    {
        var request = new CreateMaterialSupplierRequest
        {
            MaterialId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(),
            CurrentPrice = 0m,
            Currency = " vnd "
        };

        Assert.Null(CreateMaterialSupplierCommandHandler.ValidateRequest(request));
        Assert.Equal("VND", MaterialSupplierManagementRules.NormalizeCurrency(request.Currency));
    }

    [Fact]
    public void Patch_rejects_empty_body()
    {
        var error = PatchMaterialSupplierCommandHandler.ValidateRequest(
            Guid.NewGuid(),
            new PatchMaterialSupplierRequest());

        Assert.Equal("At least one editable field is required.", error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3651)]
    public void Patch_rejects_invalid_delivery_days(int days)
    {
        var error = PatchMaterialSupplierCommandHandler.ValidateRequest(
            Guid.NewGuid(),
            new PatchMaterialSupplierRequest { MinDeliveryDays = days });

        Assert.NotNull(error);
    }

    [Fact]
    public void Patch_accepts_is_active_as_the_only_editable_field()
    {
        var error = PatchMaterialSupplierCommandHandler.ValidateRequest(
            Guid.NewGuid(),
            new PatchMaterialSupplierRequest { IsActive = false });

        Assert.Null(error);
    }

    [Fact]
    public void Patch_rejects_inactive_preferred_supplier()
    {
        var error = PatchMaterialSupplierCommandHandler.ValidateRequest(
            Guid.NewGuid(),
            new PatchMaterialSupplierRequest
            {
                IsActive = false,
                IsPreferred = true
            });

        Assert.Equal("An inactive material supplier cannot be preferred.", error);
    }
}
