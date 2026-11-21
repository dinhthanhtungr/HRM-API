using HRM.Application.Abstractions.Persistence.PLM;
using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

internal sealed class SampleDeliveryCompletion(IPLMWriteDbContext db)
{
    // Called under the feedback parent lock/transaction, after all row feedback is saved.
    internal async Task<bool> ApplyAsync(SampleRequest request,
        SampleRequestSampleTrial winner, Guid actor, DateTime now, CancellationToken ct)
    {
        var formulas = await db.Formulas.Where(f => f.CompanyId == request.CompanyId &&
            f.ProductId == request.ProductId && f.IsActive).OrderBy(f => f.FormulaId).ToListAsync(ct);
        var selected = formulas.SingleOrDefault(f => f.FormulaId == winner.FormulaId);
        if (selected is null || selected.Status is not ("SampleSent" or "Rework")) return false;
        var locked = await db.Formulas.Where(f => f.FormulaId == selected.FormulaId &&
            f.CompanyId == request.CompanyId && f.ProductId == request.ProductId && f.IsActive &&
            f.UpdatedDate == selected.UpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(f => f.UpdatedDate, f => f.UpdatedDate), ct);
        if (locked != 1) return false;
        var before = JsonSerializer.SerializeToDocument(new { request.Status, request.FormulaId,
            Formulas = formulas.Select(f => new { f.FormulaId, f.Status, f.IsSelect }).ToArray() });
        winner.Formula = selected;
        SampleRequestSampleTrialApprovalRules.ApplyApproved(request, winner, formulas, actor, now,
            winner.CustomerReplyStatus!, winner.CustomerReplyNote, winner.CustomerReplyDate);
        db.AuditLogs.Add(new AuditLog {
            CompanyId = request.CompanyId, SchemaName = "SampleRequests", TableName = "SampleRequests",
            RecordId = request.SampleRequestId, CorrelationId = request.SampleRequestId,
            ActionType = AuditActionType.Update, ChangedBy = actor, ChangedAt = now,
            Reason = "Accepted sample completed request", OldValues = before,
            NewValues = JsonSerializer.SerializeToDocument(new { request.Status, request.FormulaId,
                Formulas = formulas.Select(f => new { f.FormulaId, f.Status, f.IsSelect }).ToArray() }) });
        return true;
    }
}
