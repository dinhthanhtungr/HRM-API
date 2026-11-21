using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.MaterialProcessing.Queries.LookupProcessingMaterials;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.MaterialProcessing;

public sealed class ProcessingNumericLookupPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task NumericSearchPrioritizesResinBeforeLimitOnlyForGrindingAndChopping()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        foreach (var type in new[] { typeof(Category), typeof(Material), typeof(Product), typeof(SampleRequest) })
            await CreateTable(connection, db.Model.FindEntityType(type)!);
        var user = new ProductionOrderCreationTests.TestUser { Roles = ["Developer"] };
        var category = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Category))!, new() {
            ["CategoryId"] = category, ["CompanyId"] = user.CompanyId, ["IsActive"] = true, ["Types"] = "NVL" });
        foreach (var code in Enumerable.Range(0, 55).Select(i => $"AAA026{i:00}").Concat(["NVL_NH_026", "NVL_NH_126", "NVL_NH_026-R1", "NVL_NH_026_1"]))
            await Insert(connection, db.Model.FindEntityType(typeof(Material))!, new() {
                ["MaterialId"] = Guid.NewGuid(), ["CompanyId"] = user.CompanyId, ["CategoryId"] = category,
                ["ExternalId"] = code, ["Name"] = "Material", ["Unit"] = "kg", ["IsActive"] = true });
        var handler = new LookupProcessingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user));
        foreach (var type in new[] { "B", "N" })
        {
            var rows = await handler.Handle(new("26", type), default);
            Assert.Equal(50, rows!.Count);
            Assert.Equal("NVL_NH_026-" + type, rows[0].Code);
            Assert.Equal("NVL_NH_126-" + type, rows[1].Code);
            Assert.Contains(rows, x => x.Code.StartsWith("AAA"));
        }
        Assert.StartsWith("AAA", (await handler.Handle(new("26", "L"), default))![0].Code);
        Assert.Empty((await handler.Handle(new("NVL_NH_026-R1", "N"), default))!);
        Assert.Empty((await handler.Handle(new("NVL_NH_026_1", "B"), default))!);
        Assert.StartsWith("AAA", (await handler.Handle(new("AAA", "N"), default))![0].Code);
    }
}
