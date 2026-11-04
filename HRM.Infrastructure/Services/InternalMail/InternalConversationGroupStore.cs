using HRM.Domain.Entities.InternalMailSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.Services.InternalMail;

/// <summary>Shared atomic persistence for the live writer and one-time API. No business tables except group mappings are mutated here.</summary>
internal sealed class InternalConversationGroupStore(ApplicationDbContext db)
{
    internal const int MaximumExportRows = 20000;

    internal Task<int> SchemaStateAsync(CancellationToken ct) => db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN to_regclass('"InternalMail"."InternalConversationGroups"') IS NULL THEN 0
          WHEN EXISTS (SELECT 1 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE NOT t.tgisinternal AND n.nspname='InternalMail' AND t.tgname IN ('TR_SyncLegacyConversationGroup','TR_ValidateConversationGroup')) THEN 2
          ELSE 1 END AS "Value"
        """).SingleAsync(ct);

    internal static Task LockCompanyAsync(ApplicationDbContext db, Guid company, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_advisory_xact_lock(hashtextextended({"internal-mail-group-storage:" + company.ToString("D")}, 0))
            """, ct);

    // GROUP-STORAGE-TRANSITION: legacy-adapter — source metadata still comes from legacy child fields.
    // Retain company bounds and the shared store when replacing this source after final writer cutover.
    internal Task<List<ConversationGroupSource>> SourcesAsync(Guid company, Guid[]? ids, CancellationToken ct)
    {
        var query = db.InternalConversations.AsNoTracking().Where(c => c.CompanyId == company);
        if (ids is not null)
        {
            var roots = query.Where(c => ids.Contains(c.InternalConversationId) && c.RelatedId.HasValue &&
                (c.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationTechnical ||
                 c.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationPricing ||
                 c.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationCustom)).Select(c => c.RelatedId!.Value);
            query = query.Where(c => ids.Contains(c.InternalConversationId) || roots.Contains(c.InternalConversationId));
        }
        return query.OrderBy(c => c.InternalConversationId).Select(c => new ConversationGroupSource(c.InternalConversationId,
            c.CompanyId, c.Subject, c.RelatedType, c.RelatedId, c.RelatedExternalId, c.IsActive))
            .Take(MaximumExportRows + 1).ToListAsync(ct);
    }

    internal Task<List<InternalConversationGroup>> MappingsAsync(Guid company, Guid[] ids, bool allCompany, CancellationToken ct) =>
        db.InternalConversationGroups.AsNoTracking().Where(g => (allCompany && g.CompanyId == company) || ids.Contains(g.ConversationId)).ToListAsync(ct);

    // GROUP-STORAGE-TRANSITION: legacy-write-shape — SQL 17/18/19 must match persisted legacy enum values.
    // Replace input classification only after all writers use canonical groups; do not delete atomic mapping writes.
    internal async Task InsertMissingAsync(Guid company, Guid[] ids, CancellationToken ct)
    {
        // Principal mappings before children; both statements are inside the caller's transaction/company lock.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InternalMail"."InternalConversationGroups"
              ("ConversationId","CompanyId","RootConversationId","GroupType","DisplayName","IsActive")
            SELECT c."InternalConversationId",c."CompanyId",c."InternalConversationId",0,NULL,c."IsActive"
            FROM "InternalMail"."InternalConversations" c
            WHERE c."CompanyId"={company} AND c."InternalConversationId"=ANY({ids})
              AND (c."RelatedType" IS NULL OR c."RelatedType" NOT IN (17,18,19,20))
            ON CONFLICT ("ConversationId") DO NOTHING
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InternalMail"."InternalConversationGroups"
              ("ConversationId","CompanyId","RootConversationId","GroupType","DisplayName","IsActive")
            SELECT c."InternalConversationId",c."CompanyId",c."RelatedId",
              CASE c."RelatedType" WHEN 17 THEN 1 WHEN 18 THEN 2 ELSE 3 END,
              CASE WHEN c."RelatedType"=19 THEN c."Subject"::text ELSE NULL END,c."IsActive"
            FROM "InternalMail"."InternalConversations" c
            WHERE c."CompanyId"={company} AND c."InternalConversationId"=ANY({ids}) AND c."RelatedType" IN (17,18,19)
            ON CONFLICT ("ConversationId") DO NOTHING
            """, ct);
    }

    internal Task UpdateMirrorsAsync(Guid company, Guid[] changedIds, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InternalMail"."InternalConversationGroups" g SET "IsActive"=c."IsActive",
              "DisplayName"=CASE WHEN c."RelatedType"=19 THEN c."Subject"::text ELSE g."DisplayName" END
            FROM "InternalMail"."InternalConversations" c
            WHERE c."CompanyId"={company} AND g."CompanyId"={company} AND g."ConversationId"=c."InternalConversationId"
              AND c."InternalConversationId"=ANY({changedIds})
              AND (g."IsActive" IS DISTINCT FROM c."IsActive" OR (c."RelatedType"=19 AND g."DisplayName" IS DISTINCT FROM c."Subject"::text))
            """, ct);
}
