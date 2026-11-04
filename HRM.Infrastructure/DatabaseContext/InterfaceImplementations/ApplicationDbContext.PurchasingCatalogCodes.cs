using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public async Task EnsurePurchasingCatalogCounterAsync(string prefix, bool material, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Catalog code generation requires an active transaction.");
        const string period = "GLOBAL";
        // Cùng khóa với ExternalIdServicePostgres; giữ counter toàn hệ thống như contract hiện hành.
        await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({prefix}), hashtext({period}))", cancellationToken);
        FormattableString sql;
        if (material)
            sql = $"""
                INSERT INTO public."IdCounters" ("CompanyId", "Prefix", "Period", "LastNo")
                SELECT {Guid.Empty}, {prefix}, 'GLOBAL',
                    LEAST(2147483646::numeric, COALESCE(MAX(
                        CASE WHEN substring("ExternalId" from length({prefix}) + 2) ~ '^[0-9]+$'
                        THEN substring("ExternalId" from length({prefix}) + 2)::numeric END
                    ), 0))::integer
                FROM "Material"."Materials"
                WHERE lower(left("ExternalId", length({prefix}) + 1)) = lower({prefix} || '_')
                ON CONFLICT ("CompanyId", "Prefix", "Period")
                DO UPDATE SET "LastNo" = GREATEST(public."IdCounters"."LastNo", EXCLUDED."LastNo")
                """;
        else
            sql = $"""
                INSERT INTO public."IdCounters" ("CompanyId", "Prefix", "Period", "LastNo")
                SELECT {Guid.Empty}, {prefix}, 'GLOBAL',
                    LEAST(2147483646::numeric, COALESCE(MAX(
                        CASE WHEN substring("ExternalId" from length({prefix}) + 2) ~ '^[0-9]+$'
                        THEN substring("ExternalId" from length({prefix}) + 2)::numeric END
                    ), 0))::integer
                FROM "Material"."Suppliers"
                WHERE lower(left("ExternalId", length({prefix}) + 1)) = lower({prefix} || '_')
                ON CONFLICT ("CompanyId", "Prefix", "Period")
                DO UPDATE SET "LastNo" = GREATEST(public."IdCounters"."LastNo", EXCLUDED."LastNo")
                """;
        await Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);
    }
}
