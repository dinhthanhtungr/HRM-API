using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Enums.Manufacturings;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingFormulaSourceNoteTests
{
    [Theory]
    [InlineData(null, "Product requirement", "Product requirement")]
    [InlineData("", "Product requirement", "Product requirement")]
    [InlineData(" \t\r\n", "Product requirement", "Product requirement")]
    [InlineData(" VU note ", "Product requirement", " VU note ")]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData(null, "  ", "  ")]
    public async Task VuNote_UsesProductRequirementOnlyWhenFormulaNoteIsBlank(
        string? note, string? requirement, string? expected)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.FromVu);
        f.User.Roles = [ApplicationRoles.Developer];
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order);
        var vu = f.Db.Formulas.Single();
        vu.Note = note;
        vu.Product.Requirement = requirement;
        var permissions = new CurrentUserPermissionService(f.User);
        var (displayDb, displayStore) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        displayStore.Seed(nameof(displayDb.Materials), f.Material);
        var handler = new GetProductionOrderFormulaSourceQueryHandler(
            f.Db, displayDb, f.User, permissions, new PLMFieldVisibilityService(permissions));

        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, vu.FormulaId), default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, result.Data!.Note);
        Assert.Equal(note, vu.Note);
    }
}
