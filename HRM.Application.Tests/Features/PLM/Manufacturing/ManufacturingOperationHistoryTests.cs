using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingOperationHistory;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.ShiftReportSchema;
using HRM.Domain.Enums.Manufacturings;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingOperationHistoryTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("foreign-order")]
    [InlineData("unrelated")]
    [InlineData("foreign-code")]
    [InlineData("inactive")]
    [InlineData("permission")]
    public async Task History_RequiresScopedSourceAndUnambiguousCompanyBeforeReadingKeylessRows(string reason)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.FromVu);
        var va = f.Db.ManufacturingFormulas.Single();
        f.Store.Seed(nameof(f.Db.MfgProductionOrders), f.Order);
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion {
            CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId,
            ManufacturingFormulaId = va.ManufacturingFormulaId, ManufacturingFormula = va, MfgProductionOrder = f.Order,
            ValidFrom = ManufacturingFormulaCreationTests.Fixture.Now });
        var older = ManufacturingFormulaCreationTests.Fixture.Now.AddDays(-1);
        f.Store.Seed(nameof(f.Db.OperationHistoryMds), new OperationHistoryMd { ExternalId = va.ExternalId, CreatedAt = older, Set1 = 180, Act1 = 185 },
            new OperationHistoryMd { ExternalId = va.ExternalId, CreatedAt = older.AddHours(1), Set13 = 200, Act13 = 210 },
            new OperationHistoryMd { ExternalId = "OTHER", CreatedAt = older.AddDays(5) });
        if (reason == "foreign-order") f.Order.CompanyId = Guid.NewGuid();
        if (reason == "inactive") va.IsActive = false;
        if (reason == "permission") { f.User.HasExplicitPermissionSet = true; f.User.Permissions = []; }
        if (reason == "foreign-code") f.Store.Seed(nameof(f.Db.ManufacturingFormulas), va, new ManufacturingFormula {
            ManufacturingFormulaId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), ExternalId = va.ExternalId, IsActive = false });
        var handler = new GetManufacturingOperationHistoryQueryHandler(f.Db, f.User, new CurrentUserPermissionService(f.User));
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId,
            reason == "unrelated" ? Guid.NewGuid() : va.ManufacturingFormulaId, PageSize: 1), default);
        Assert.Equal(reason == "valid", result.Success);
        if (result.Success)
        {
            Assert.True(result.Data!.HasHistory);
            Assert.Equal(older.AddHours(1), result.Data.LatestCreatedAt);
            Assert.Equal(2, result.Data.History.TotalCount);
            var row = Assert.Single(result.Data.History.Items);
            Assert.Equal(13, row.Zones.Count);
            Assert.Equal(210, row.Zones[12].Actual);
        }
        Assert.Equal(0, f.Store.Saves);
    }
}
