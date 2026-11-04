using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Category;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed partial class PurchasingCatalogManagementTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public async Task MfgCategoryRead_RequiresBothCapabilitiesAndAuthentication(bool viewOrder, bool viewMaterials, bool authenticated)
    {
        var grants = new List<string>();
        if (viewOrder) grants.Add(ApplicationPermissions.PLM.ViewProductionOrders);
        if (viewMaterials) grants.Add(ApplicationPermissions.PLM.ViewFormulaMaterials);
        var f = new Fixture(new() { HasExplicitPermissionSet = true, Permissions = grants.ToArray(), IsAuthenticated = authenticated });
        if (!(viewOrder && viewMaterials && authenticated))
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetCategoriesAsync(default));
            return;
        }
        var own = new Category { CategoryId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value, IsActive = true, Name = "Pigment" };
        f.Store.Seed(nameof(f.Db.Categories), own,
            new Category { CategoryId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true, Name = "Foreign" });
        Assert.Equal(own.CategoryId, Assert.Single(await f.Service.GetCategoriesAsync(default)).CategoryId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetUnitsAsync(default));
    }

    [Theory]
    [InlineData(CategoryTypes.Product)]
    [InlineData(CategoryTypes.NVL)]
    [InlineData(CategoryTypes.VTBT)]
    [InlineData(null)]
    public async Task Categories_FilterTypeBeforeReturning_KeepActiveCompanyScopeAndOrder(CategoryTypes? type)
    {
        var f = new Fixture();
        Category Row(string name, string? kind, bool active = true, Guid? company = null) => new()
        {
            CategoryId = Guid.NewGuid(), Name = name, Types = kind,
            IsActive = active, CompanyId = company ?? f.User.CompanyId!.Value
        };
        var rows = new[] { Row("C", "Product"), Row("B", "NVL"), Row("A", "VTBT"), Row("D", null) };
        f.Store.Seed(nameof(f.Db.Categories), rows.Concat(new[]
        {
            Row("Hidden", type?.ToString() ?? "NVL", false),
            Row("Foreign", type?.ToString() ?? "NVL", company: Guid.NewGuid())
        }).ToArray());
        var result = await f.Service.GetCategoriesAsync(default, type);
        Assert.Equal(rows.Where(x => type == null || x.Types == type.ToString()).OrderBy(x => x.Name)
            .Select(x => x.CategoryId), result.Select(x => x.CategoryId));
    }

    [Theory]
    [InlineData("Sale")]
    [InlineData("QC")]
    public async Task Categories_TypeFilterDoesNotGrantAccess(string role)
    {
        var f = new Fixture(new() { Roles = [role] });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetCategoriesAsync(default, CategoryTypes.NVL));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(999)]
    public void Categories_UndefinedEnumFailsRequestValidation(int value)
    {
        var query = new CatalogCategoryOptionsQuery { Type = (CategoryTypes)value };
        Assert.False(Validator.TryValidateObject(query, new ValidationContext(query), [], true));
    }
}
