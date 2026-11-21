using System.Diagnostics;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.MaterialProcessing.Queries.LookupProcessingMaterials;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.MaterialProcessing;

public sealed class ProcessingLookupPostgresTests(IsolatedGroupStoragePostgres postgres, ITestOutputHelper output)
    : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task LookupPreservesDuplicateScopeAndMeasuresLegacyAgainstGroupedQuery()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await CreateTable(connection, db.Model.FindEntityType(typeof(Category))!);
        await CreateTable(connection, db.Model.FindEntityType(typeof(Material))!);
        await CreateTable(connection, db.Model.FindEntityType(typeof(HRM.Domain.Entities.SampleRequestSchema.Product))!);
        await CreateTable(connection, db.Model.FindEntityType(typeof(HRM.Domain.Entities.SampleRequestSchema.SampleRequest))!);
        var company = Guid.NewGuid();
        var category = Guid.NewGuid();
        await Insert(connection, db.Model.FindEntityType(typeof(Category))!, new() {
            [nameof(Category.CategoryId)] = category, [nameof(Category.CompanyId)] = company,
            [nameof(Category.IsActive)] = true, [nameof(Category.Types)] = "NVL" });
        var materialModel = db.Model.FindEntityType(typeof(Material))!;
        async Task Add(string code, Guid? scope = null, bool active = true, string unit = "kg")
            => await Insert(connection, materialModel, new() {
                [nameof(Material.MaterialId)] = Guid.NewGuid(), [nameof(Material.CompanyId)] = scope ?? company,
                [nameof(Material.CategoryId)] = category, [nameof(Material.ExternalId)] = code,
                [nameof(Material.Name)] = "Benchmark resin", [nameof(Material.Unit)] = unit,
                [nameof(Material.IsActive)] = active });
        await Add("SEED");
        // Chỉ dữ liệu tổng hợp trong cluster loopback cô lập; không đọc cấu hình hoặc DB API.
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO "Material"."Materials"
            SELECT (jsonb_populate_record(NULL::"Material"."Materials", to_jsonb(s) ||
                jsonb_build_object('MaterialId', md5(i::text)::uuid,
                    'ExternalId', 'NVL_' || lpad(i::text, 5, '0')))).*
            FROM "Material"."Materials" s CROSS JOIN generate_series(1, 5000) i
            WHERE s."ExternalId" = 'SEED'
            """, connection)) await seed.ExecuteNonQueryAsync();
        await Add("DUP"); await Add(" dup ", unit: "g");
        await Add("KEEP"); await Add(" keep ", scope: Guid.NewGuid()); await Add("KEEP", active: false);
        await Add("BAD-N"); await Add("BAD_N");
        var user = new LookupUser(company);
        var handler = new LookupProcessingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user));
        var duplicate = await handler.Handle(new("DUP", "N"), default);
        Assert.Empty(duplicate!);
        Assert.Single((await handler.Handle(new("KEEP", "N"), default))!);
        Assert.Empty((await handler.Handle(new("BAD", "N"), default))!);
        Assert.Empty((await handler.Handle(new("%", "N"), default))!);
        Assert.Null(await new LookupProcessingMaterialsQueryHandler(db, user with { Role = "SaleUser" },
            new CurrentUserPermissionService(user with { Role = "SaleUser" })).Handle(new("", "N"), default));

        // Baseline: chính predicate NOT EXISTS tương quan trước tối ưu, cùng giới hạn 50 và dữ liệu.
        var legacy = db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive == true &&
            x.MaterialId != Guid.Empty && x.Unit != null && x.Unit.Trim().ToLower() == "kg" && x.ExternalId != null &&
            x.ExternalId.Trim() != "" && x.ExternalId.Length <= 256 && x.ExternalId.Trim().Length <= 254 &&
            (x.Name == null || x.Name.Length <= 1000) && x.Category.CompanyId == company && x.Category.IsActive == true && x.Category.Types == "NVL" &&
            !db.Materials.Any(other => other.MaterialId != x.MaterialId && other.CompanyId == company && other.IsActive == true &&
                other.Category.CompanyId == company && other.Category.IsActive == true && other.Category.Types == "NVL" &&
                other.ExternalId != null && other.ExternalId.Trim().ToUpper() == x.ExternalId.Trim().ToUpper()) &&
            !x.ExternalId.Trim().ToUpper().EndsWith("-B") && !x.ExternalId.Trim().ToUpper().EndsWith("-N") && !x.ExternalId.Trim().ToUpper().EndsWith("-L") &&
            !x.ExternalId.Trim().ToUpper().EndsWith("_B") && !x.ExternalId.Trim().ToUpper().EndsWith("_N") && !x.ExternalId.Trim().ToUpper().EndsWith("_L"))
            .OrderBy(x => x.ExternalId).ThenBy(x => x.MaterialId).Take(50);
        async Task<List<Guid>> Before()
        {
            var roots = await legacy.ToListAsync();
            var codes = roots.Select(x => x.ExternalId!.Trim().ToUpper() + "-N").ToList();
            _ = await db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.ExternalId != null && codes.Contains(x.ExternalId.Trim().ToUpper())).ToListAsync();
            return roots.Select(x => x.MaterialId).ToList();
        }
        var beforeIds = await Before(); // warm-up both query paths, excluded from samples
        _ = await handler.Handle(new("", "N"), default);
        var oldMs = new List<double>(); var newMs = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            Assert.Equal(beforeIds, await Before());
            oldMs.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            var request = new LookupProcessingMaterialsQuery("", "N");
            var after = await handler.Handle(request, default);
            newMs.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(beforeIds, after!.Select(x => x.BaseMaterialId!.Value));
            Assert.Contains("mpf_roots;dur=", request.ServerTiming);
            Assert.Contains("mpf_variants;dur=", request.ServerTiming);
        }
        output.WriteLine($"Isolated PostgreSQL; 5000 generated roots; 3 warm samples; before_ms=[{string.Join(',', oldMs.Select(x => x.ToString("F2")))}]; after_ms=[{string.Join(',', newMs.Select(x => x.ToString("F2")))}]");
    }

    private sealed record LookupUser(Guid Scope) : ICurrentUser
    {
        public string Role { get; init; } = "Developer";
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId => Scope;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => Role == role;
    }
}
