using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Services.PLM.ConversationSummary;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

/// <summary>Fresh guarded loopback cluster only. Never reads appsettings or live connection strings.</summary>
public sealed class ConversationSummaryPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    private static readonly Guid Company = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Request = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private const string Type = "SampleRequestConversationSummarySync";
    private static readonly IOptions<ConversationSummaryOptions> Enabled = Options.Create(new ConversationSummaryOptions { Enabled = true });

    [PostgresGroupStorageFact]
    public async Task WorkerIsSilentScopedIdempotentLatestSourceAndTransactional()
    {
        await SchemaAsync();
        await using var connection = await postgres.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
        async Task Process()
        {
            await using var context = new ApplicationDbContext(options, Enabled);
            Assert.True(await new SampleRequestConversationSummaryStore(context, Enabled).ProcessOneAsync(default));
        }
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            var store = new SampleRequestConversationSummaryStore(db, Enabled);
            Assert.Equal("Historical", (await store.ReadAsync(Company, Request, default)).Status);
            var page = await store.EnqueuePageAsync(Company, null, 100, Request, default);
            Assert.Equal(1, page.Queued); Assert.False(page.HasMore);
            await store.EnqueuePageAsync(Company, null, 100, Request, default);
            Assert.Equal("Pending", (await store.ReadAsync(Company, Request, default)).Status);
        }
        // A queued job carries no old content: it must use this newer committed value.
        await postgres.ExecuteAsync("""UPDATE "SampleRequests"."SampleRequests" SET "SaleComment"='Newest'""");
        async Task CompetingWorker()
        {
            await using var peer = await postgres.OpenAsync();
            await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(peer).Options, Enabled);
            await new SampleRequestConversationSummaryStore(context, Enabled).ProcessOneAsync(default);
        }
        await Task.WhenAll(CompetingWorker(), CompetingWorker());
        DateTime? firstSynced;
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            var store = new SampleRequestConversationSummaryStore(db, Enabled);
            var dto = await store.ReadAsync(Company, Request, default);
            Assert.Equal("Synced", dto.Status); Assert.Contains(dto.Fields, f => f.Code == "saleComment" && f.Value == "Newest");
            firstSynced = dto.SyncedAt;
            Assert.Equal(1, await db.SampleRequestConversationSummaries.CountAsync());
            Assert.All(await db.OutboxMessages.ToArrayAsync(), job => Assert.NotNull(job.ProcessedAt));
            Assert.Equal("Unavailable", (await store.ReadAsync(Guid.NewGuid(), Request, default)).Status);
            Assert.Equal(2, (await store.StatusAsync(Company, default)).Completed);
            Assert.Equal(0, (await store.StatusAsync(Guid.NewGuid(), default)).Completed);
            await store.EnqueuePageAsync(Company, null, 1, Request, default);
        }
        await Process();
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            Assert.Equal(firstSynced, (await new SampleRequestConversationSummaryStore(db, Enabled).ReadAsync(Company, Request, default)).SyncedAt);
            // Tracked source + queue roll back together, no request-time projection building.
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var sample = new SampleRequest { SampleRequestId = Request, CompanyId = Company, SaleComment = "Rollback" };
                db.Attach(sample); db.Entry(sample).Property(x => x.SaleComment).IsModified = true;
                await db.SaveChangesAsync();
                Assert.True(await db.OutboxMessages.AnyAsync(x => x.ProcessedAt == null));
                await tx.RollbackAsync();
            }
        }
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            Assert.False(await db.OutboxMessages.AnyAsync(x => x.ProcessedAt == null));
            var sample = new SampleRequest { SampleRequestId = Request, CompanyId = Company, SaleComment = "Persisted" };
            db.Attach(sample); db.Entry(sample).Property(x => x.SaleComment).IsModified = true;
            await db.SaveChangesAsync();
        }
        await Process();
        // Product fan-out is keyed and bounded; no startup/global sweep.
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            var product = new Product { ProductId = ProductId, CompanyId = Company, ColourCode = "UPDATED" };
            db.Attach(product); db.Entry(product).Property(x => x.ColourCode).IsModified = true;
            await db.SaveChangesAsync();
        }
        await Process(); await Process();
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            var dto = await new SampleRequestConversationSummaryStore(db, Enabled).ReadAsync(Company, Request, default);
            Assert.Contains(dto.Fields, f => f.Code == "colourCode" && f.Value == "UPDATED");
            Assert.Equal(1, await db.SampleRequestConversationSummaries.CountAsync());
        }
        // Bypassed writers cannot trick the UI into claiming old content is current.
        await postgres.ExecuteAsync("""UPDATE "SampleRequests"."SampleRequests" SET "SaleComment"='Bypassed'""");
        await using (var db = new ApplicationDbContext(options, Enabled))
            Assert.Equal("OutOfDate", (await new SampleRequestConversationSummaryStore(db, Enabled).ReadAsync(Company, Request, default)).Status);
        await postgres.ExecuteAsync("""UPDATE "SampleRequests"."SampleRequests" SET "RequestType"='private'""");
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            var store = new SampleRequestConversationSummaryStore(db, Enabled);
            Assert.Empty((await store.ReadAsync(Company, Request, default)).Fields);
            await store.EnqueuePageAsync(Company, null, 1, Request, default);
        }
        await Process();
        await using (var db = new ApplicationDbContext(options, Enabled)) Assert.Empty(await db.SampleRequestConversationSummaries.ToArrayAsync());
        await ExerciseBoundedFanoutAsync(options, Process);
    }

    private async Task ExerciseBoundedFanoutAsync(DbContextOptions<ApplicationDbContext> options, Func<Task> process)
    {
        await postgres.ExecuteAsync("""
          INSERT INTO "SampleRequests"."SampleRequests" ("SampleRequestId","CompanyId","IsActive","ExternalId","CustomerId","ProductId","RequestType","BagWeight","Package")
          SELECT md5('summary-synthetic-' || i)::uuid,'10000000-0000-0000-0000-000000000001',true,'SYNTHETIC',
            '40000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','Báo giá',0,'' FROM generate_series(1,201) AS i;
          """);
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            db.OutboxMessages.Add(new OutboxMessage { Type = Type,
                PayloadJson = JsonSerializer.Serialize(new { CompanyId = Company, EntityId = ProductId, Kind = "Product", Cursor = (Guid?)null, Version = 1 }) });
            await db.SaveChangesAsync();
        }
        await process();
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            // 100 request jobs + ONE cursor continuation, never the entire product scope at once.
            Assert.Equal(101, await db.OutboxMessages.CountAsync(x => x.ProcessedAt == null));
        }
        for (var i = 0; i < 220; i++)
        {
            await using var db = new ApplicationDbContext(options, Enabled);
            if (!await new SampleRequestConversationSummaryStore(db, Enabled).ProcessOneAsync(default)) break;
        }
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            Assert.Equal(201, await db.SampleRequestConversationSummaries.CountAsync());
            var store = new SampleRequestConversationSummaryStore(db, Enabled);
            var page = await store.EnqueuePageAsync(Company, null, 999, null, default);
            Assert.Equal(100, page.Queued); Assert.True(page.HasMore); Assert.NotNull(page.NextCursor);
            var next = await store.EnqueuePageAsync(Company, page.NextCursor, 100, null, default);
            Assert.Equal(100, next.Queued); Assert.True(next.HasMore);
            Assert.NotEqual(page.NextCursor, next.NextCursor);
        }
        for (var i = 0; i < 210; i++)
        {
            await using var db = new ApplicationDbContext(options, Enabled);
            if (!await new SampleRequestConversationSummaryStore(db, Enabled).ProcessOneAsync(default)) break;
        }
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            Assert.Equal(201, await db.SampleRequestConversationSummaries.CountAsync());
            db.OutboxMessages.Add(new OutboxMessage { Type = Type,
                PayloadJson = JsonSerializer.Serialize(new { CompanyId = Company, EntityId = Request, Kind = "Request", Cursor = (Guid?)null, Version = 999 }) });
            await db.SaveChangesAsync();
        }
        for (var i = 0; i < 5; i++) await process();
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            var store = new SampleRequestConversationSummaryStore(db, Enabled);
            Assert.False(await store.ProcessOneAsync(default));
            var failed = await db.OutboxMessages.SingleAsync(x => x.ProcessedAt == null);
            Assert.Equal(5, failed.Attempts); Assert.Equal("ConversationSummary.SyncFailed", failed.Error);
            Assert.Equal(1, (await store.StatusAsync(Company, default)).Failed);
            var activeId = await db.SampleRequestConversationSummaries.Select(x => x.SampleRequestId).FirstAsync();
            db.OutboxMessages.Add(new OutboxMessage { Type = Type, Attempts = 5,
                PayloadJson = JsonSerializer.Serialize(new { CompanyId = Company, EntityId = activeId, Kind = "Request", Cursor = (Guid?)null, Version = 1 }) });
            await db.SaveChangesAsync();
            await store.EnqueuePageAsync(Company, null, 1, activeId, default);
        }
        await process();
        await using (var db = new ApplicationDbContext(options, Enabled))
        {
            // A newly queued retry clears the older failed job with the same valid target.
            Assert.Single(await db.OutboxMessages.Where(x => x.ProcessedAt == null).ToArrayAsync());
            Assert.Equal(201, await db.SampleRequestConversationSummaries.CountAsync());
        }
    }

    [Fact]
    public async Task DisabledDoesNotOpenAnyConnectionOrProjectionTable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1").Options;
        await using var db = new ApplicationDbContext(options);
        var store = new SampleRequestConversationSummaryStore(db, Options.Create(new ConversationSummaryOptions()));
        Assert.Equal("Disabled", (await store.ReadAsync(Company, Request, default)).Status);
        Assert.False((await store.EnqueuePageAsync(Company, null, 100, null, default)).Enabled);
        Assert.False((await store.StatusAsync(Company, default)).Enabled);
        Assert.False(await store.ProcessOneAsync(default));
    }

    private Task SchemaAsync() => postgres.ExecuteAsync("""
        CREATE SCHEMA IF NOT EXISTS "SampleRequests";
        CREATE SCHEMA IF NOT EXISTS "Customer";
        CREATE SCHEMA IF NOT EXISTS notification;
        CREATE TABLE "Customer"."Customer" ("CustomerId" uuid PRIMARY KEY, "CompanyId" uuid, "ExternalId" citext);
        CREATE TABLE "SampleRequests"."Products" (
          "ProductId" uuid PRIMARY KEY, "CompanyId" uuid, "IsActive" boolean, "Name" citext, "ColourCode" citext,
          "ColourName" text, "PolymerMatchedIn" text, "Application" text, "ProductUsage" text, "Requirement" text,
          "WeatherResistance" text, "LightCondition" text, "VisualTest" text);
        CREATE TABLE "SampleRequests"."SampleRequests" (
          "SampleRequestId" uuid PRIMARY KEY, "CompanyId" uuid, "IsActive" boolean, "ExternalId" text,
          "CustomerId" uuid, "ProductId" uuid, "RequestType" text, "ExpectedQuantity" double precision,
          "SampleQuantity" double precision, "Package" text, "BagWeight" integer, "CustomerProductCode" text,
          "RequestDeliveryDate" timestamp, "ExpectedDeliveryDate" timestamp, "RequestTestSampleDate" timestamp,
          "ExpectedPriceQuoteDate" timestamp, "SaleComment" text, "AdditionalComment" text, "UpdatedDate" timestamp);
        CREATE TABLE "SampleRequests"."SampleRequestConversationSummaries" (
          "CompanyId" uuid, "SampleRequestId" uuid, "ContentJson" jsonb, "FormatVersion" integer,
          "AppliedSourceFingerprint" varchar(64), "SyncedAt" timestamp, "Status" varchar(24), "LastErrorCode" varchar(80),
          PRIMARY KEY ("CompanyId", "SampleRequestId"));
        CREATE TABLE notification.outbox_messages (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
          type varchar(128), payload_json jsonb, created_at timestamp, processed_at timestamp, attempts integer, error text);
        INSERT INTO "Customer"."Customer" VALUES ('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','NORMAL');
        INSERT INTO "SampleRequests"."Products" ("ProductId","CompanyId","IsActive","Name","ColourCode")
          VALUES ('30000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',true,'Synthetic','TEST');
        INSERT INTO "SampleRequests"."SampleRequests" ("SampleRequestId","CompanyId","IsActive","ExternalId","CustomerId","ProductId","RequestType","BagWeight","Package","SaleComment")
          VALUES ('20000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',true,'SYNTHETIC',
          '40000000-0000-0000-0000-000000000001','30000000-0000-0000-0000-000000000001','Báo giá',0,'','Initial');
        """);
}
