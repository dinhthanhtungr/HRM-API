using HRM.Application.Commons.Authorization;
using HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.ExcelSpecial;

public sealed class ManufacturingVaExportPostgresTests
{
    // Opt-in only; all connections enforce PostgreSQL read-only, never migrate/seed/save.
    [ReadOnlyPostgresFact]
    public async Task Export_TranslatesOnPostgres_RespectsDatesAndForeignCompany()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ReadOnlyPostgresFactAttribute.Variable)) {
            Options = "-c default_transaction_read_only=on -c statement_timeout=30000", CommandTimeout = 30
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        var latest = await db.ManufacturingFormulas.AsNoTracking().Where(x => x.IsActive && EF.Functions.ILike(x.ExternalId, "VA%"))
            .OrderByDescending(x => x.CreatedDate).Select(x => new { x.CompanyId, x.CreatedDate }).FirstAsync();
        var user = User(latest.CompanyId);
        var capture = new ManufacturingVaExportTests.CaptureRenderer();
        var handler = new ExportManufacturingVaQueryHandler(db, db, user, new CurrentUserPermissionService(user),
            new ManufacturingVaExportTests.Visibility(true), capture);
        var start = new DateTime(latest.CreatedDate.Year, latest.CreatedDate.Month, 1);
        var result = await handler.Handle(new() { From = start, To = latest.CreatedDate.Date }, default);
        Assert.True(result.Success);
        Assert.NotEmpty(capture.Rows!);
        Assert.All(capture.Rows!, row => Assert.InRange(row.CreatedDate, start, latest.CreatedDate.Date.AddDays(1).AddTicks(-1)));
        Assert.Equal(capture.Rows!.Count, capture.Rows.Select(x => x.VaCode).Distinct().Count());
        Assert.All(capture.Rows.Where(x => x.IsSingleMaterial), row => Assert.Equal("TM", row.ProductionType));
        var mfgCodes = capture.Rows.Select(x => x.MfgCode).Where(x => x.Length > 0).Distinct().ToArray();
        var productionQuantities = await db.MfgProductionOrders.AsNoTracking()
            .Where(x => x.CompanyId == latest.CompanyId && x.IsActive && mfgCodes.Contains(x.ExternalId))
            .Select(x => new { x.ExternalId, x.TotalQuantity }).ToDictionaryAsync(x => x.ExternalId, x => x.TotalQuantity);
        Assert.All(capture.Rows.Where(x => x.MfgCode.Length > 0),
            row => Assert.Equal(productionQuantities[row.MfgCode], row.PlannedQuantity));
        var foreign = User(Guid.NewGuid());
        var foreignCapture = new ManufacturingVaExportTests.CaptureRenderer();
        var foreignHandler = new ExportManufacturingVaQueryHandler(db, db, foreign, new CurrentUserPermissionService(foreign),
            new ManufacturingVaExportTests.Visibility(true), foreignCapture);
        Assert.True((await foreignHandler.Handle(new(), default)).Success);
        Assert.Empty(foreignCapture.Rows!);
    }

    private static IqcWorkflowTests.User User(Guid company) => new() {
        CompanyId = company, HasExplicitPermissionSet = true,
        Permissions = [ApplicationPermissions.PLM.ViewProductionOrders]
    };
}

public sealed class ReadOnlyPostgresFactAttribute : FactAttribute
{
    internal const string Variable = "HRM_VA_EXPORT_READONLY_CONNECTION";
    public ReadOnlyPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = "Set the explicit read-only PostgreSQL test connection to run this integration check.";
    }
}
