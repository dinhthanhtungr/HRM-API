using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Workflow;

/// <summary>Read-only review of historical jobs. Never publishes or changes business state.</summary>
internal static class WorkflowNotificationReplayAssessment
{
    internal static async Task<Dictionary<Guid, string>> ReadAsync(IPLMDbContext db, Guid company,
        IReadOnlyCollection<SampleRequestWorkflowEvent> events, CancellationToken ct)
    {
        var result = events.ToDictionary(e => e.Id, Initial);
        var candidates = events.Where(e => result[e.Id] == "CheckFormula").ToArray();
        if (candidates.Length == 0) return result;
        var ids = candidates.Select(e => e.Id).ToArray();
        var requestIds = candidates.Select(e => e.SampleRequestId).Distinct().ToArray();
        var samples = await db.SampleRequests.AsNoTracking().Where(s => s.CompanyId == company && requestIds.Contains(s.SampleRequestId))
            .Select(s => new { s.SampleRequestId, s.IsActive, s.Status, s.FormulaId }).ToDictionaryAsync(s => s.SampleRequestId, ct);
        var messages = await db.InternalMessages.AsNoTracking().Where(m => ids.Contains(m.InternalMessageId) &&
            m.Conversation.CompanyId == company).Select(m => m.InternalMessageId).ToHashSetAsync(ct);
        var superseded = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company && ids.Contains(e.Id) &&
            db.SampleRequestWorkflowEvents.Any(newer => newer.CompanyId == company && newer.SampleRequestId == e.SampleRequestId &&
                newer.EventType == e.EventType && newer.RecordedAt > e.RecordedAt)).Select(e => e.Id).ToHashSetAsync(ct);
        var approved = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(e => e.CompanyId == company && ids.Contains(e.Id) &&
            db.SampleRequestWorkflowEvents.Any(later => later.CompanyId == company && later.SampleRequestId == e.SampleRequestId &&
                later.FormulaId == e.FormulaId && later.EventType == SampleFlowRules.StandardPriceApproved && later.RecordedAt >= e.RecordedAt))
            .Select(e => e.Id).ToHashSetAsync(ct);
        foreach (var e in candidates)
        {
            samples.TryGetValue(e.SampleRequestId, out var sample);
            result[e.Id] = Formula(messages.Contains(e.Id), sample?.IsActive == true,
                superseded.Contains(e.Id), sample is not null && SampleFlowRules.IsOpen(sample.Status),
                e.FormulaId.HasValue && sample?.FormulaId == e.FormulaId);
            if (result[e.Id] == "Ready" && approved.Contains(e.Id)) result[e.Id] = "PriceAlreadyApproved";
        }
        return result;
    }

    internal static string Initial(SampleRequestWorkflowEvent evt)
        => evt.NotificationId.HasValue || evt.MessageId.HasValue ? "AlreadyPublished"
            : !SampleRequestWorkflowPublicationRules.ShouldPublishMessage(evt) ? "Suppressed"
            : evt.EventType == SampleRequestWorkflowEvents.Created ? "SummaryOnly"
            : evt.EventType == SampleRequestWorkflowEvents.FormulaConfirmed ? "CheckFormula" : "ManualReview";

    internal static string Formula(bool hasMessage, bool active, bool superseded, bool open, bool sameFormula)
        => hasMessage ? "AlreadyPublished" : !active ? "Unavailable" : superseded ? "Superseded"
            : !open ? "RequestClosed" : !sameFormula ? "FormulaChanged" : "Ready";
}
