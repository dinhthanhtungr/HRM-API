using HRM.Application.Features.PLM.SampleRequests.Dtos;
using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Services.ConversationSummary;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Services.PLM.ConversationSummary;

public sealed class SampleRequestConversationSummaryStore(ApplicationDbContext db, IOptions<ConversationSummaryOptions> options)
    : ISampleRequestConversationSummaryStore
{
    private const int PageSize = 100;
    public async Task<SummarySyncStatus> StatusAsync(Guid company, CancellationToken ct)
    {
        if (!options.Value.Enabled) return new(false, 0, 0, 0);
        var scope = JsonSerializer.Serialize(new { CompanyId = company });
        var groups = await db.OutboxMessages.AsNoTracking().Where(x => x.Type == SummarySyncJob.Type &&
                EF.Functions.JsonContains(x.PayloadJson, scope))
            .GroupBy(x => x.ProcessedAt != null ? "Completed" : x.Attempts >= 5 ? "Failed" : "Pending")
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        int Count(string status) => groups.SingleOrDefault(x => x.Status == status)?.Count ?? 0;
        return new(true, Count("Pending"), Count("Failed"), Count("Completed"));
    }
    public async Task<ConversationSummaryDto> ReadAsync(Guid company, Guid sampleRequest, CancellationToken ct)
    {
        if (!options.Value.Enabled) return new("Disabled", null, []);
        var source = await SourceAsync(company, sampleRequest, ct);
        if (source is null) return new("Unavailable", null, []);
        var row = await db.SampleRequestConversationSummaries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == company && x.SampleRequestId == sampleRequest, ct);
        var current = row?.ContentJson is not null && row.FormatVersion == ConversationSummaryContent.Version &&
            row.AppliedSourceFingerprint == ConversationSummaryContent.Fingerprint(source);
        if (current) return new("Synced", row!.SyncedAt, JsonSerializer.Deserialize<SummaryField[]>(row.ContentJson!) ?? []);
        var payload = JsonSerializer.Serialize(new SummarySyncJob(company, sampleRequest, "Request"));
        var pending = db.OutboxMessages.AsNoTracking().Where(x => x.Type == SummarySyncJob.Type && x.ProcessedAt == null && x.PayloadJson == payload);
        var hasPending = await pending.AnyAsync(x => x.Attempts < 5, ct);
        var failed = !hasPending && await pending.AnyAsync(x => x.Attempts >= 5, ct);
        if (row?.ContentJson is null) return new(hasPending ? "Pending" : failed ? "Failed" : "Historical", null, []);
        return new(hasPending ? "Pending" : failed ? "Failed" : "OutOfDate", row.SyncedAt,
            row.FormatVersion == ConversationSummaryContent.Version ? JsonSerializer.Deserialize<SummaryField[]>(row.ContentJson) ?? [] : []);
    }

    public async Task<SummaryBackfillPage> EnqueuePageAsync(Guid company, Guid? cursor, int take, Guid? sampleRequest, CancellationToken ct)
    {
        if (!options.Value.Enabled) return new(0, null, false, false);
        take = Math.Clamp(take, 1, PageSize);
        var query = db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive);
        if (sampleRequest.HasValue) query = query.Where(x => x.SampleRequestId == sampleRequest.Value);
        if (cursor.HasValue) query = query.Where(x => x.SampleRequestId.CompareTo(cursor.Value) > 0);
        var ids = await query.OrderBy(x => x.SampleRequestId).Select(x => x.SampleRequestId).Take(take + 1).ToArrayAsync(ct);
        foreach (var id in ids.Take(take)) db.OutboxMessages.Add(new SummarySyncJob(company, id, "Request").ToOutbox());
        await db.SaveChangesAsync(ct);
        return new(Math.Min(ids.Length, take), ids.Length == 0 ? cursor : ids[Math.Min(ids.Length, take) - 1], ids.Length > take);
    }

    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return false;
        var candidate = await db.OutboxMessages.AsNoTracking().Where(x => x.Type == SummarySyncJob.Type && x.ProcessedAt == null && x.Attempts < 5)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new { x.Id, x.PayloadJson }).FirstOrDefaultAsync(ct);
        if (candidate is null) return false;
        var id = candidate.Id;
        Guid company;
        try { company = JsonSerializer.Deserialize<SummarySyncJob>(candidate.PayloadJson)?.CompanyId ?? Guid.Empty; }
        catch (JsonException) { company = Guid.Empty; }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Lock order is company THEN job, including coalesced rows: no worker holds a row while waiting for its company.
        var lockKey = "sample-summary:" + company.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM notification.outbox_messages WHERE id={id} FOR UPDATE", ct);
        var row = await db.OutboxMessages.SingleAsync(x => x.Id == id, ct);
        if (row.Type != SummarySyncJob.Type || row.ProcessedAt.HasValue || row.Attempts >= 5) return true;
        try
        {
            var job = JsonSerializer.Deserialize<SummarySyncJob>(row.PayloadJson);
            if (job is null || job.Version != 1 || job.CompanyId == Guid.Empty || job.CompanyId != company || job.EntityId == Guid.Empty || job.Kind is not ("Request" or "Product"))
                throw new InvalidOperationException("Invalid summary job.");
            // Snapshot duplicate IDs BEFORE reading source. Jobs committed later remain pending.
            var duplicates = await db.OutboxMessages.AsNoTracking().Where(x => x.Type == SummarySyncJob.Type &&
                    x.ProcessedAt == null && x.PayloadJson == row.PayloadJson)
                .OrderBy(x => x.Id).Select(x => x.Id).Take(100).ToArrayAsync(ct);
            if (job.Kind == "Product") await FanOutAsync(job, ct);
            else await RefreshAsync(job.CompanyId, job.EntityId, ct);
            row.Attempts++; row.Error = null; row.ProcessedAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            await db.OutboxMessages.Where(x => duplicates.Contains(x.Id) && x.ProcessedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ProcessedAt, row.ProcessedAt).SetProperty(x => x.Error, (string?)null), ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(ct);
            await tx.DisposeAsync();
            db.ChangeTracker.Clear();
            await db.OutboxMessages.Where(x => x.Id == id && x.ProcessedAt == null && x.Attempts < 5)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.Error, "ConversationSummary.SyncFailed"), ct);
            return true;
        }
    }

    private async Task FanOutAsync(SummarySyncJob job, CancellationToken ct)
    {
        var ids = await db.SampleRequests.AsNoTracking().Where(x => x.CompanyId == job.CompanyId && x.ProductId == job.EntityId &&
                (!job.Cursor.HasValue || x.SampleRequestId.CompareTo(job.Cursor.Value) > 0))
            .OrderBy(x => x.SampleRequestId).Select(x => x.SampleRequestId).Take(PageSize + 1).ToArrayAsync(ct);
        foreach (var id in ids.Take(PageSize)) db.OutboxMessages.Add(new SummarySyncJob(job.CompanyId, id, "Request").ToOutbox());
        if (ids.Length > PageSize) db.OutboxMessages.Add((job with { Cursor = ids[PageSize - 1] }).ToOutbox());
    }

    private async Task RefreshAsync(Guid company, Guid id, CancellationToken ct)
    {
        var content = await SourceAsync(company, id, ct);
        var row = await db.SampleRequestConversationSummaries.SingleOrDefaultAsync(x => x.CompanyId == company && x.SampleRequestId == id, ct);
        if (content is null) { if (row is not null) db.Remove(row); return; }
        var fingerprint = ConversationSummaryContent.Fingerprint(content);
        if (row?.AppliedSourceFingerprint == fingerprint && row.FormatVersion == ConversationSummaryContent.Version) return;
        if (row is null) db.SampleRequestConversationSummaries.Add(row = new() { CompanyId = company, SampleRequestId = id });
        row.ContentJson = content; row.AppliedSourceFingerprint = fingerprint;
        row.FormatVersion = ConversationSummaryContent.Version; row.SyncedAt = DateTime.Now;
        row.Status = "Synced"; row.LastErrorCode = null;
    }

    private async Task<string?> SourceAsync(Guid company, Guid id, CancellationToken ct)
    {
        var source = await db.SampleRequests.AsNoTracking().Where(s => s.CompanyId == company && s.SampleRequestId == id && s.IsActive &&
                s.Product.CompanyId == company && s.Product.IsActive && s.Customer.CompanyId == company)
            .Select(s => new {
                CustomerCode = s.Customer.ExternalId,
                Request = new SampleRequest { ExternalId = s.ExternalId, RequestType = s.RequestType,
                    ExpectedQuantity = s.ExpectedQuantity, SampleQuantity = s.SampleQuantity, Package = s.Package, BagWeight = s.BagWeight,
                    CustomerProductCode = s.CustomerProductCode, RequestDeliveryDate = s.RequestDeliveryDate,
                    ExpectedDeliveryDate = s.ExpectedDeliveryDate, RequestTestSampleDate = s.RequestTestSampleDate,
                    ExpectedPriceQuoteDate = s.ExpectedPriceQuoteDate, SaleComment = s.SaleComment, AdditionalComment = s.AdditionalComment },
                Product = new Product { Name = s.Product.Name, ColourCode = s.Product.ColourCode, ColourName = s.Product.ColourName,
                    PolymerMatchedIn = s.Product.PolymerMatchedIn, Application = s.Product.Application, ProductUsage = s.Product.ProductUsage,
                    Requirement = s.Product.Requirement, WeatherResistance = s.Product.WeatherResistance,
                    LightCondition = s.Product.LightCondition, VisualTest = s.Product.VisualTest }
            }).SingleOrDefaultAsync(ct);
        return source is null || ConversationSummaryContent.Suppressed(source.Request.RequestType, source.CustomerCode) ? null :
            ConversationSummaryContent.Serialize(ConversationSummaryContent.Build(source.Request, source.Product));
    }
}
