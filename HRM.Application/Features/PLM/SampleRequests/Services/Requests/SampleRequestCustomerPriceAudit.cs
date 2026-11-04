using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

// Tên lớp giữ tương thích code cũ; các query runtime chỉ đọc SampleRequestWorkflowEvents.
internal static class SampleRequestCustomerPriceAudit
{
    public const string Created = "SampleRequestWorkflowCreated";
    public const string Accepted = "CustomerPriceAccepted";
    public const string Revoked = "CustomerPriceAcceptanceRevoked";
    private static readonly string[] EventReasons = [Accepted, Revoked, "CustomerPriceConfirmed", "CustomerPriceConfirmationRevoked",
        Services.Workflow.SampleFlowRules.PriceRejected, Services.Workflow.SampleFlowRules.PriceNoResponse];

    public static IQueryable<SampleRequestWorkflowEvent> Events(IPLMDbContext db, Guid company, Guid id) =>
        db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
            x.SampleRequestId == id && EventReasons.Contains(x.EventType));

    public static Task<SampleRequestWorkflowEvent?> LatestAsync(IPLMDbContext db, Guid company, Guid id, CancellationToken ct) =>
        Events(db, company, id).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);

    public static Task<bool> IsManagedAsync(IPLMDbContext db, Guid company, Guid id, CancellationToken ct) =>
        db.SampleRequestWorkflowEvents.AsNoTracking().AnyAsync(x => x.CompanyId == company &&
            x.SampleRequestId == id && (x.EventType == Created || EventReasons.Contains(x.EventType)), ct);

    public static SampleRequestWorkflowEvent CreateWorkflowMarker(Guid id, Guid company, Guid employee, DateTime now, string status) =>
        SampleRequestWorkflowEvents.Create(id, company, employee, now, Created);

    public static SampleRequestWorkflowEvent CreateEvent(Guid id, Guid company, Guid employee, DateTime now, bool revoke,
        string fromStatus, string toStatus, DateTime? customerAcceptedAt, Guid? lineId, string? reference, string? note) => new()
    {
        CompanyId = company, SampleRequestId = id,
        RecordedByEmployeeId = employee, RecordedAt = now, EventType = revoke ? Revoked : Accepted,
        OccurredAt = revoke ? now : customerAcceptedAt ?? now,
        QuotationLineId = lineId, PriceReference = reference, Note = note
    };

    public static CustomerPriceConfirmationDto Read(SampleRequestWorkflowEvent entry, string? employeeName = null,
        SampleRequestWorkflowEvent? acceptedEvent = null) => new(
        entry.EventType is Accepted or "CustomerPriceConfirmed", entry.RecordedAt, entry.RecordedByEmployeeId,
        employeeName, entry.EventType is Accepted or "CustomerPriceConfirmed" ? entry.OccurredAt : acceptedEvent?.OccurredAt,
        entry.QuotationLineId, entry.PriceReference, entry.Note, entry.Id);

    public static CustomerPriceConfirmationDto Read(AuditLog audit, string? employeeName = null)
    {
        var root = audit.NewValues?.RootElement;
        JsonElement? Field(params string[] names)
        {
            if (root is not { ValueKind: JsonValueKind.Object } value) return null;
            foreach (var name in names)
                if (value.TryGetProperty(name, out var field)) return field;
            return null;
        }
        string? Text(params string[] names) => Field(names) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
        var date = Field("CustomerAcceptedAt", "customerAcceptedAt");
        var line = Field("QuotationLineId", "quotationLineId", "CustomerPriceQuotationLineId");
        return new CustomerPriceConfirmationDto(audit.Reason is Accepted or "CustomerPriceConfirmed",
            audit.ChangedAt, audit.ChangedBy, employeeName,
            date is { ValueKind: JsonValueKind.String } d && d.TryGetDateTime(out var acceptedAt) ? acceptedAt : null,
            line is { ValueKind: JsonValueKind.String } l && l.TryGetGuid(out var lineId) ? lineId : null,
            Text("PriceReference", "priceReference", "CustomerPriceReference"),
            Text("Note", "note", "RevocationReason", "CustomerPriceConfirmationNote"));
    }

    public static async Task AttachAsync(IPLMDbContext db, Guid company,
        IReadOnlyList<SampleRequestSampleTrialReportDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return;
        var ids = items.Select(x => x.SampleRequestId).Distinct().ToArray();
        var source = db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company && ids.Contains(x.SampleRequestId));
        var managed = (await source.Where(x => x.EventType == Created || EventReasons.Contains(x.EventType))
            .Select(x => x.SampleRequestId).Distinct().ToListAsync(ct)).ToHashSet();
        var events = await source.Where(x => EventReasons.Contains(x.EventType)).GroupBy(x => x.SampleRequestId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).First()).ToListAsync(ct);
        var byRequest = events.ToDictionary(x => x.SampleRequestId);
        var relatedIds = events.Where(x => x.RelatedEventId.HasValue).Select(x => x.RelatedEventId!.Value).ToArray();
        var related = await source.Where(x => relatedIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var employees = events.Where(x => x.RecordedByEmployeeId.HasValue).Select(x => x.RecordedByEmployeeId!.Value).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && employees.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.FullName }).ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct);
        foreach (var item in items)
        {
            item.RequiresCustomerPriceConfirmation = (SampleRequestCustomerPriceRules.IsColourSet(item.InfoType) || SampleRequestCustomerPriceRules.IsQuotation(item.InfoType) || item.RequestedSampleQuantity is > 0) &&
                (item.SampleRequestStatus == "WaitingCustomerPriceConfirmation" || managed.Contains(item.SampleRequestId));
            if (!byRequest.TryGetValue(item.SampleRequestId, out var audit)) continue;
            var dto = Read(audit, audit.RecordedByEmployeeId is { } employee && names.TryGetValue(employee, out var name) ? name : null,
                audit.RelatedEventId is { } relatedId ? related.GetValueOrDefault(relatedId) : null);
            item.CustomerPriceConfirmation = dto;
            // Giữ flat fields cho client cũ; tất cả được đọc từ sự kiện, không query cột prototype.
            if (!dto.IsActive) continue;
            item.CustomerPriceConfirmedAt = dto.RecordedAt;
            item.CustomerPriceConfirmedByEmployeeId = dto.RecordedByEmployeeId;
            item.CustomerPriceConfirmedByName = dto.RecordedByName;
            item.CustomerPriceQuotationLineId = dto.QuotationLineId;
            item.CustomerPriceReference = dto.PriceReference;
            item.CustomerPriceConfirmationNote = dto.Note;
        }
    }
}
