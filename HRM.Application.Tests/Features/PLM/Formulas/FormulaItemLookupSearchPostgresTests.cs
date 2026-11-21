using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Features.PLM.Materials.Queries.GetFormulaItemLookup;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Application.Tests.Features.PLM.MaterialProcessing;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaItemLookupSearchPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ExplicitProcessingIsScopedLiteralPagedAndReusesSavedProduct()
    {
        await using var connection = await postgres.OpenAsync();
        var commands = new List<string>();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).LogTo(commands.Add, [RelationalEventId.CommandExecuted]).Options);
        foreach (var type in new[] { typeof(Category), typeof(Material), typeof(Product), typeof(SampleRequest),
                     typeof(Formula), typeof(MaterialPurchaseAvailability) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);

        var user = new ProductionOrderCreationTests.TestUser { Roles = ["Developer"] };
        var category = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Category))!, new() {
            ["CategoryId"] = category, ["CompanyId"] = user.CompanyId, ["IsActive"] = true,
            ["Types"] = "NVL", ["ExternalId"] = "NH" });
        // Equal dates/names exercise the identity tie-breaker across pages.
        foreach (var i in Enumerable.Range(0, 62))
            await Insert(connection, db.Model.FindEntityType(typeof(Material))!, new() {
                ["MaterialId"] = Guid.NewGuid(), ["CompanyId"] = i == 60 ? Guid.NewGuid() : user.CompanyId,
                ["IsActive"] = i != 61, ["CategoryId"] = category, ["ExternalId"] = $"NVL_NH_009{i:00}",
                ["Name"] = "Resin", ["Unit"] = "kg", ["CreatedDate"] = new DateTime(2026, 11, 13) });
        var handler = new GetFormulaItemLookupQueryHandler(db, user,
            System.Reflection.DispatchProxy.Create<IMaterialPriceQueryService, ProcessingCreationPostgresTests.Prices>());

        commands.Clear();
        var ordinary = await handler.Handle(new() { CategoryId = category }, default);
        Assert.Equal(60, ordinary.TotalCount);
        Assert.DoesNotContain(ordinary.Items, x => x.IsProcessingPreview);
        // Empty search no longer executes the two correlated processing anti-join branches.
        Assert.DoesNotContain(commands, command => command.Contains("~") || command.Contains("-N") || command.Contains("-B"));

        var ids = new HashSet<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await handler.Handle(new() { Keyword = "009 Băm", CategoryId = category, PageNumber = page, PageSize = 15 }, default);
            Assert.Equal(60, result.TotalCount);
            Assert.Equal(15, result.Items.Count);
            Assert.Equal(page < 4, result.HasNextPage);
            Assert.All(result.Items, item => {
                Assert.True(item.IsProcessingPreview);
                Assert.EndsWith("-B", item.ExternalId);
                Assert.True(ids.Add(item.ItemId), "Duplicate across pages");
            });
        }
        Assert.Empty((await handler.Handle(new() { Keyword = "009%_ Băm", CategoryId = category }, default)).Items);
        Assert.Empty((await handler.Handle(new() { Keyword = "009 Băm", CategoryId = Guid.NewGuid() }, default)).Items);
        var first = (await handler.Handle(new() { Keyword = "00900 nghien", CategoryId = category }, default)).Items.Single();
        Assert.EndsWith("-N", first.ExternalId);

        var productId = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Product))!, new() {
            ["ProductId"] = productId, ["CompanyId"] = user.CompanyId, ["IsActive"] = true,
            ["CategoryId"] = category, ["Name"] = "Resin (Nghiền)", ["ColourCode"] = "NVL_NH_00900-N", ["Unit"] = "kg" });
        await Insert(connection, db.Model.FindEntityType(typeof(SampleRequest))!, new() {
            ["SampleRequestId"] = Guid.NewGuid(), ["ProductId"] = productId, ["CompanyId"] = user.CompanyId,
            ["IsActive"] = true, ["RequestType"] = "SemiFinished", ["ExternalId"] = "NVL_NH_00900-N" });
        var saved = await handler.Handle(new() { Keyword = "00900-N", CategoryId = category }, default);
        Assert.Equal(1, saved.TotalCount);
        var option = Assert.Single(saved.Items);
        Assert.Equal(productId, option.ItemId);
        Assert.False(option.IsProcessingPreview);

        // Existing legacy material codes also suppress the virtual choice and stay selectable.
        var legacyMaterialId = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Material))!, new() {
            ["MaterialId"] = legacyMaterialId, ["CompanyId"] = user.CompanyId, ["IsActive"] = true,
            ["CategoryId"] = category, ["ExternalId"] = "NVL_NH_00901-B", ["Name"] = "Resin (Băm)", ["Unit"] = "kg" });
        var legacy = await handler.Handle(new() { Keyword = "00901 bam", CategoryId = category }, default);
        Assert.Equal(1, legacy.TotalCount);
        Assert.Equal(legacyMaterialId, Assert.Single(legacy.Items).ItemId);
        Assert.False(legacy.Items[0].IsProcessingPreview);
    }
}
