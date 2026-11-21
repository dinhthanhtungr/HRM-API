using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Services.ExternalIds;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.PLM.CustomerLabels;

public sealed class CustomerLabelCounterPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task ConcurrentTemporaryCodesUsePersistentCustomerScopedNumbers()
    {
        // Fresh guarded loopback test cluster; never uses API config or production credentials.
        await using (var connection = await postgres.OpenAsync())
        {
            await using var command = new NpgsqlCommand("""
                CREATE TABLE public."IdCounters" (
                    "CompanyId" uuid NOT NULL, "Prefix" text NOT NULL, "Period" text NOT NULL, "LastNo" integer NOT NULL,
                    PRIMARY KEY ("CompanyId", "Prefix", "Period"));
                """, connection);
            await command.ExecuteNonQueryAsync();
        }
        var company = Guid.NewGuid(); var customer = Guid.NewGuid();
        var prefix = $"CL:{company:N}:{customer:N}";
        async Task<string> Allocate(string scope)
        {
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            return await new ExternalIdServicePostgres(db).GenerateGlobalCodeAsync(company, scope);
        }
        var codes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Allocate(prefix)));
        Assert.Equal(12, codes.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 12), codes.Select(c => int.Parse(c[(c.LastIndexOf('_') + 1)..])).OrderBy(x => x));
        Assert.Equal(prefix + "_13", await Allocate(prefix));
        Assert.EndsWith("_1", await Allocate($"CL:{company:N}:{Guid.NewGuid():N}"));
    }
}

