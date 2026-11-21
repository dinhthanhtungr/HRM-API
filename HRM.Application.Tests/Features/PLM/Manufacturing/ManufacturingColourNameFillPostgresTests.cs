using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.FillManufacturingColourNames;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingColourNameFillPostgresTests(IsolatedGroupStoragePostgres postgres)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ApplyIsNullOnlyCompanyScopedAuditedAndIdempotent()
    {
        await using var connection = await postgres.OpenAsync();
        var user = await Seed(connection);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var log = new ManufacturingColourNameFillTests.Log();
        var handler = new FillManufacturingColourNamesCommandHandler(db, user, new CurrentUserPermissionService(user), new ManufacturingColourNameFillTests.Clock(), log);
        var preview = await handler.Handle(new(), default);
        Assert.True(preview.Success, preview.Message); Assert.Equal(2, preview.Data!.Matched); Assert.Empty(log.Items);
        var result = await handler.Handle(new(false), default);
        Assert.True(result.Success, result.Message); Assert.Equal(2, result.Data!.Updated); Assert.Equal(2, log.Items.Count);
        Assert.All(log.Items, x => Assert.Contains("NULL → Red", x.Note));
        var rows = await db.MfgProductionOrders.AsNoTracking().OrderBy(x => x.ExternalId).Select(x => new { x.ExternalId, x.ColorName, x.UpdatedBy }).ToListAsync();
        Assert.Equal("Red", rows.Single(x => x.ExternalId == "A").ColorName);
        Assert.Equal(user.EmployeeId, rows.Single(x => x.ExternalId == "A").UpdatedBy);
        Assert.Equal("", rows.Single(x => x.ExternalId == "EMPTY").ColorName);
        Assert.Equal("Old", rows.Single(x => x.ExternalId == "SAVED").ColorName);
        Assert.Null(rows.Single(x => x.ExternalId == "FOREIGN").ColorName);
        Assert.Equal(0, (await handler.Handle(new(false), default)).Data!.Updated);
    }

    [PostgresGroupStorageFact]
    public async Task RecheckPreservesANonNullSnapshotChangedAfterPreviewPage()
    {
        await using var connection = await postgres.OpenAsync();
        var user = await Seed(connection);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var log = new ManufacturingColourNameFillTests.Log
        {
            AfterFirst = async () => { await db.MfgProductionOrders.Where(x => x.ExternalId == "B").ExecuteUpdateAsync(s => s.SetProperty(x => x.ColorName, "Manual")); }
        };
        var result = await new FillManufacturingColourNamesCommandHandler(db, user, new CurrentUserPermissionService(user), new ManufacturingColourNameFillTests.Clock(), log).Handle(new(false), default);
        Assert.True(result.Success, result.Message); Assert.Equal(1, result.Data!.Updated); Assert.Equal(1, result.Data.ConcurrentlySkipped);
        Assert.Equal("Manual", await db.MfgProductionOrders.Where(x => x.ExternalId == "B").Select(x => x.ColorName).SingleAsync());
    }

    [PostgresGroupStorageFact]
    public async Task SourceColourChangedAfterPreviewPageIsSkipped()
    {
        await using var connection = await postgres.OpenAsync();
        var user = await Seed(connection);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var log = new ManufacturingColourNameFillTests.Log
        {
            AfterFirst = async () => { await db.Products.ExecuteUpdateAsync(s => s.SetProperty(x => x.ColourName, "Blue")); }
        };
        var handler = new FillManufacturingColourNamesCommandHandler(db, user, new CurrentUserPermissionService(user), new ManufacturingColourNameFillTests.Clock(), log);
        var result = await handler.Handle(new(false), default);
        Assert.Equal(1, result.Data!.Updated); Assert.Equal(1, result.Data.ConcurrentlySkipped);
        Assert.Null(await db.MfgProductionOrders.Where(x => x.ExternalId == "B").Select(x => x.ColorName).SingleAsync());
        Assert.Equal(1, (await handler.Handle(new(false), default)).Data!.Updated);
        Assert.Equal("Red", await db.MfgProductionOrders.Where(x => x.ExternalId == "A").Select(x => x.ColorName).SingleAsync());
    }

    [PostgresGroupStorageFact]
    public async Task ApplyWalksMultiplePagesWithoutLosingNullCandidates()
    {
        await using var connection = await postgres.OpenAsync();
        var user = await Seed(connection);
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO manufacturing."MfgProductionOrders"
              ("mfgProductionOrderId",external_id,product_id,company_id,is_active,color_name,status)
            SELECT md5('extra-' || n)::uuid, 'EXTRA-' || n,
              '10000000-0000-0000-0000-000000000001'::uuid,@company,true,NULL,'New'
            FROM generate_series(1,251) n;
            """, connection))
        {
            seed.Parameters.AddWithValue("company", user.CompanyId!.Value);
            await seed.ExecuteNonQueryAsync();
        }
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var result = await new FillManufacturingColourNamesCommandHandler(db, user, new CurrentUserPermissionService(user), new ManufacturingColourNameFillTests.Clock(), new ManufacturingColourNameFillTests.Log()).Handle(new(false), default);
        Assert.Equal(253, result.Data!.Examined); Assert.Equal(253, result.Data.Updated);
        Assert.Equal(100, result.Data.Items.Count); Assert.True(result.Data.HasMoreItems);
    }

    [PostgresGroupStorageFact]
    public async Task AuditFailureRollsBackSnapshotUpdates()
    {
        await using var connection = await postgres.OpenAsync();
        var user = await Seed(connection);
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var handler = new FillManufacturingColourNamesCommandHandler(db, user, new CurrentUserPermissionService(user), new ManufacturingColourNameFillTests.Clock(), new ManufacturingColourNameFillTests.Log { Throw = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(false), default));
        Assert.Null(await db.MfgProductionOrders.Where(x => x.ExternalId == "A").Select(x => x.ColorName).SingleAsync());
    }

    private static async Task<ProductionOrderCreationTests.TestUser> Seed(NpgsqlConnection connection)
    {
        var user = new ProductionOrderCreationTests.TestUser { Roles = [ApplicationRoles.Lab.LabUser] };
        await using var command = new NpgsqlCommand("""
            CREATE EXTENSION IF NOT EXISTS citext;
            CREATE SCHEMA IF NOT EXISTS manufacturing;
            CREATE SCHEMA IF NOT EXISTS "SampleRequests";
            CREATE TABLE IF NOT EXISTS "SampleRequests"."Products" (
              "ProductId" uuid PRIMARY KEY, "CompanyId" uuid, "IsActive" boolean, "ColourName" citext);
            CREATE TABLE IF NOT EXISTS manufacturing."MfgProductionOrders" (
              "mfgProductionOrderId" uuid PRIMARY KEY, external_id citext, product_id uuid,
              product_name_snapshot citext, status text, company_id uuid, is_active boolean,
              color_name citext, updated_by uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000', updated_date timestamp without time zone);
            TRUNCATE manufacturing."MfgProductionOrders", "SampleRequests"."Products";
            INSERT INTO "SampleRequests"."Products" VALUES
              ('10000000-0000-0000-0000-000000000001', @company, true, 'Red');
            INSERT INTO manufacturing."MfgProductionOrders"
              ("mfgProductionOrderId", external_id,product_id,company_id,is_active,color_name,status) VALUES
              ('00000000-0000-0000-0000-000000000001','A','10000000-0000-0000-0000-000000000001',@company,true,NULL,'Scheduled'),
              ('00000000-0000-0000-0000-000000000002','B','10000000-0000-0000-0000-000000000001',@company,true,NULL,'Done'),
              ('00000000-0000-0000-0000-000000000003','EMPTY','10000000-0000-0000-0000-000000000001',@company,true,'','New'),
              ('00000000-0000-0000-0000-000000000004','SAVED','10000000-0000-0000-0000-000000000001',@company,true,'Old','New'),
              ('00000000-0000-0000-0000-000000000005','FOREIGN','10000000-0000-0000-0000-000000000001',@other,true,NULL,'New');
            """, connection);
        command.Parameters.AddWithValue("company", user.CompanyId!.Value);
        command.Parameters.AddWithValue("other", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
        return user;
    }
}
