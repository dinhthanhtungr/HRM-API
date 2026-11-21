using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("Developer", false)]
    [InlineData("LabUser", false)]
    [InlineData("SaleUser", false)]
    [InlineData("Developer", true)]
    [InlineData("LabUser", true)]
    [InlineData("SaleUser", true)]
    public async Task SampleAllConversationsExecuteWithMetadataAndQuickGroupsOnIsolatedPostgres(string role, bool groupStorage)
        => await MeasureSampleAllAsync(role, groupStorage, 8);

    [PostgresGroupStorageFact]
    public async Task SampleAllCanonicalHighHistoryExecutesOnIsolatedPostgres()
        => await MeasureSampleAllAsync("LabUser", true, 64);

    private async Task MeasureSampleAllAsync(string role, bool groupStorage, int eventCount, bool compareUuidJoin = false,
        bool compareIconRanking = false, bool compareUnread = false, bool compareGroupsAndKeys = false, bool compareSummary = false)
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync("""
            ALTER TABLE hr."Employees" ADD COLUMN IF NOT EXISTS "FullName" text DEFAULT 'Synthetic';
            CREATE TABLE IF NOT EXISTS "SampleRequests"."SampleRequestWorkflowEvents"
              ("CompanyId" uuid,"MessageId" uuid,"EventType" text,"NotificationId" uuid);
            ALTER TABLE "SampleRequests"."SampleRequests"
              ADD COLUMN IF NOT EXISTS "CustomerId" uuid, ADD COLUMN IF NOT EXISTS "ManagerBy" uuid;
            ALTER TABLE "SampleRequests"."Products" ADD COLUMN IF NOT EXISTS "IsActive" boolean DEFAULT true;
            ALTER TABLE "Material"."Categories"
              ADD COLUMN IF NOT EXISTS "Name" text, ADD COLUMN IF NOT EXISTS "IsActive" boolean DEFAULT true,
              ADD COLUMN IF NOT EXISTS "Types" text DEFAULT 'Product';
            CREATE TABLE IF NOT EXISTS "Customer"."Customer"
              ("CustomerId" uuid,"CompanyId" uuid,"ExternalId" text,"CustomerName" text);
            INSERT INTO "Customer"."Customer" VALUES
              ('92000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','SYNTHETIC','Synthetic');
            INSERT INTO "Material"."Categories" ("CategoryId","CompanyId","ExternalId","Name","Types","IsActive") VALUES
              ('93000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','CMP','Synthetic','Product',true);
            UPDATE "SampleRequests"."Products" SET "CategoryId"='93000000-0000-0000-0000-000000000001';
            UPDATE "InternalMail"."InternalConversations" SET "RelatedType"=16,"RelatedId"=NULL;
            INSERT INTO "SampleRequests"."SampleRequests"
              ("SampleRequestId","CompanyId","ProductId","ExternalId","IsActive","CustomerId","ManagerBy")
            SELECT md5('sample-' || i)::uuid,'10000000-0000-0000-0000-000000000001',
              '91000000-0000-0000-0000-000000000001','SYNTHETIC',true,
              '92000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001'
              FROM generate_series(1,160) AS i;
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","RelatedId","CreatedBy")
            SELECT md5('sample-root-' || i)::uuid,'10000000-0000-0000-0000-000000000001','Synthetic sample',5,
              md5('sample-' || i)::uuid,'00000000-0000-0000-0000-000000000001' FROM generate_series(1,160) AS i;
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","RelatedId","CreatedBy")
            SELECT md5('sample-tech-' || i)::uuid,'10000000-0000-0000-0000-000000000001','Synthetic technical',17,
              md5('sample-root-' || i)::uuid,'00000000-0000-0000-0000-000000000001' FROM generate_series(1,160) AS i;
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","RelatedId","CreatedBy")
            SELECT md5('sample-price-' || i)::uuid,'10000000-0000-0000-0000-000000000001','Synthetic pricing',18,
              md5('sample-root-' || i)::uuid,'00000000-0000-0000-0000-000000000001' FROM generate_series(1,80) AS i;
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","CreatedBy")
            SELECT md5('sample-other-' || i)::uuid,'10000000-0000-0000-0000-000000000001','Synthetic other',16,
              '00000000-0000-0000-0000-000000000001' FROM generate_series(1,200) AS i;
            INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId")
            SELECT "InternalConversationId",'00000000-0000-0000-0000-000000000001'
              FROM "InternalMail"."InternalConversations" WHERE "Subject" LIKE 'Synthetic%';
            INSERT INTO "InternalMail"."InternalMessages"
              ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
            SELECT md5(c."InternalConversationId"::text || '-message')::uuid,c."InternalConversationId",
              '00000000-0000-0000-0000-000000000002','Synthetic',
              '2026-11-05'::timestamp + (row_number() OVER (ORDER BY c."InternalConversationId")) * interval '1 minute'
              FROM "InternalMail"."InternalConversations" c WHERE c."Subject" LIKE 'Synthetic%';
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId")
            SELECT "InternalMessageId",'00000000-0000-0000-0000-000000000001'
              FROM "InternalMail"."InternalMessages" WHERE "Body"='Synthetic';
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
            SELECT md5(c."InternalConversationId"::text || '-event-' || j)::uuid,c."CompanyId",
              jsonb_build_object('conversationId',c."InternalConversationId",'messageId',md5(c."InternalConversationId"::text || '-message')::uuid),
              '2026-11-05'::timestamp + ((row_number() OVER (ORDER BY c."InternalConversationId")) * 10 + j) * interval '1 minute',38
              FROM "InternalMail"."InternalConversations" c CROSS JOIN generate_series(1,8) AS j
              WHERE c."Subject" LIKE 'Synthetic%';
            INSERT INTO notification.notification_user_states (notification_id,user_id,is_read)
            SELECT n.id,'00000000-0000-0000-0000-000000000001',false FROM notification.notifications n
              WHERE n.created_date >= '2026-11-05';
            ANALYZE;
            """.Replace("generate_series(1,8)", $"generate_series(1,{eventCount})"));
        await using var connection = await postgres.OpenAsync();
        if (groupStorage)
            await postgres.ExecuteAsync("""
                INSERT INTO "InternalMail"."InternalConversationGroups"
                  ("ConversationId","CompanyId","RootConversationId","GroupType","DisplayName","IsActive")
                SELECT "InternalConversationId","CompanyId","InternalConversationId",0,NULL,"IsActive"
                  FROM "InternalMail"."InternalConversations" WHERE "RelatedType" NOT IN (17,18,19) OR "RelatedType" IS NULL;
                INSERT INTO "InternalMail"."InternalConversationGroups"
                  ("ConversationId","CompanyId","RootConversationId","GroupType","DisplayName","IsActive")
                SELECT "InternalConversationId","CompanyId","RelatedId",
                  (CASE WHEN "RelatedType"=17 THEN 1 WHEN "RelatedType"=18 THEN 2 ELSE 3 END)::smallint,NULL,"IsActive"
                  FROM "InternalMail"."InternalConversations" WHERE "RelatedType" IN (17,18,19);
                ANALYZE;
                """);
        await using (var roleBinding = new Npgsql.NpgsqlCommand("""
            UPDATE public."AspNetRoles" SET "Name"=@role,"NormalizedName"=upper(@role)
            WHERE "Id"='b1000000-0000-0000-0000-000000000001'
            """, connection))
        {
            roleBinding.Parameters.AddWithValue("role", role);
            await roleBinding.ExecuteNonQueryAsync();
        }
        var commands = new QuotationCommandMetrics();
        var uuidJoin = new SampleUuidJoinInterceptor();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).AddInterceptors(commands, uuidJoin).Options);
        db.Database.SetCommandTimeout(10);
        var user = new UnreadPerformanceUser(role);
        var areas = new InternalMailAreaAccessService(db, user,
            Microsoft.Extensions.Options.Options.Create(new InternalMailGroupStorageOptions { Enabled = groupStorage }));
        if (compareSummary) { await CompareSummaryAsync(db, areas, role, groupStorage); return; }
        var handler = new GetInternalConversationsQueryHandler(db, user,
            new InternalConversationSampleRequestInfoResolver(db), new InternalConversationQuotationInfoResolver(db), areas);
        var request = new GetInternalConversationsQuery
        {
            RelatedType = InternalMailRelatedType.SampleRequest, Archived = false,
            UnreadOnly = false, PageNumber = 1, PageSize = 50
        };
        var first = await handler.Handle(request, default);
        Assert.Equal(400, first.TotalCount);
        Assert.Equal(50, first.Items.Count);
        Assert.All(first.Items, item =>
        {
            Assert.NotNull(item.SampleRequestInfo);
            Assert.NotNull(item.SampleRequestInfo!.ProductCategory);
            Assert.NotEmpty(item.QuickGroups);
            Assert.Equal(eventCount, item.UnreadCount);
        });
        // The default standalone query is the reference when the handler starts reusing page counts.
        var referenceGroups = await areas.QuickGroupsAsync(first.Items.Select(i => i.GroupConversationId), default);
        Assert.All(first.Items, item => Assert.Equal(
            JsonSerializer.Serialize(referenceGroups.GetValueOrDefault(item.GroupConversationId)),
            JsonSerializer.Serialize(item.QuickGroups)));
        if (compareUnread)
        {
            await CompareSampleUnreadAsync(db, areas, handler, user, role, groupStorage, eventCount, compareGroupsAndKeys);
            return;
        }
        if (compareIconRanking)
        {
            await CompareSampleIconRankingAsync(db, areas, handler, request,
                first.Items.Select(i => i.ConversationId).ToArray(), postgres.RepositoryRoot);
            return;
        }
        var expectedPresentation = first.Items.Select(item => JsonSerializer.Serialize(new
        {
            item.ConversationId, item.Subject, item.DisplayTitle, item.SampleRequestInfo,
            item.UnreadCount, item.QuickGroups, item.RepresentativeTopicCode, item.RepresentativeEventGroupCode
        })).ToArray();
        var elapsed = new List<double>();
        var sqlRuns = new List<IReadOnlyList<QuotationCommandMetric>>();
        for (var sample = 0; sample < 5; sample++)
        {
            commands.Items.Clear();
            var timer = Stopwatch.StartNew();
            var result = await handler.Handle(request, default);
            elapsed.Add(timer.Elapsed.TotalMilliseconds);
            Assert.Equal(first.Items.Select(i => i.ConversationId), result.Items.Select(i => i.ConversationId));
            Assert.Equal(first.Items.Select(i => i.UnreadCount), result.Items.Select(i => i.UnreadCount));
            Assert.Equal(expectedPresentation, result.Items.Select(item => JsonSerializer.Serialize(new
            {
                item.ConversationId, item.Subject, item.DisplayTitle, item.SampleRequestInfo,
                item.UnreadCount, item.QuickGroups, item.RepresentativeTopicCode, item.RepresentativeEventGroupCode
            })));
            sqlRuns.Add(commands.Items.ToArray());
        }
        var destination = Path.Combine(postgres.RepositoryRoot, "artifacts", "sample-all-performance");
        Directory.CreateDirectory(destination);
        var suffix = eventCount == 8 ? "" : ".history-" + eventCount;
        await File.WriteAllTextAsync(Path.Combine(destination, role + (groupStorage ? ".canonical" : "") + suffix + ".json"), JsonSerializer.Serialize(new
        {
            role, groupStorage, eventCount, roots = 160, technicalChildren = 160, pricingChildren = 80, unrelatedRoots = 200,
            samples = 5, medianMs = elapsed.Order().ElementAt(2), minMs = elapsed.Min(), maxMs = elapsed.Max(), commands = sqlRuns
        }));
        if (compareUuidJoin)
            await CompareSampleUuidJoinAsync(db, handler, request, uuidJoin, postgres.RepositoryRoot);
    }
}
