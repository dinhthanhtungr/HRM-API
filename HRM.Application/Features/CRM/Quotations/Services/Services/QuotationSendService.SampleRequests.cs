using System.Text.Json;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Audits;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    // Persisted with the successful Sale mark-sent action, including pending confirmation.
    private async Task SyncLinkedSampleQuoteDatesAsync(Quotation quotation, Guid actorId,
        DateTime sentAt, CancellationToken cancellationToken)
    {
        var lines = quotation.Lines.Where(x => x.IsActive && x.SampleRequestId.HasValue &&
            x.SampleRequestId != Guid.Empty).ToArray();
        if (lines.Length == 0) return;
        var ids = lines.Select(x => x.SampleRequestId!.Value).Distinct().ToArray();
        var requests = await _writeDbContext.SampleRequests.AsTracking()
            .Where(x => ids.Contains(x.SampleRequestId) && x.CompanyId == quotation.CompanyId &&
                x.CustomerId == quotation.CustomerId && x.IsActive &&
                x.Status != "Completed" && x.Status != "Cancelled" &&
                (!x.RealPriceQuoteDate.HasValue || x.RealPriceQuoteDate < sentAt))
            .ToListAsync(cancellationToken);
        foreach (var request in requests)
        {
            if (!lines.Any(x => x.SampleRequestId == request.SampleRequestId && x.ProductId == request.ProductId))
                continue;
            var oldDate = request.RealPriceQuoteDate;
            request.RealPriceQuoteDate = sentAt;
            request.UpdatedBy = actorId;
            request.UpdatedDate = QuotationWorkflowRules.NextUpdatedDate(sentAt, request.UpdatedDate);
            _writeDbContext.AuditLogs.Add(new AuditLog
            {
                CompanyId = request.CompanyId, SchemaName = "SampleRequests", TableName = "SampleRequests",
                RecordId = request.SampleRequestId, ActionType = AuditActionType.Update,
                ChangedBy = actorId, ChangedAt = sentAt, CorrelationId = quotation.QuotationId,
                Reason = $"QuotationSent:{quotation.QuotationId}",
                OldValues = JsonSerializer.SerializeToDocument(new { RealPriceQuoteDate = oldDate }),
                NewValues = JsonSerializer.SerializeToDocument(new { RealPriceQuoteDate = sentAt }),
                ChangedValues = JsonSerializer.SerializeToDocument(new { RealPriceQuoteDate = new { Old = oldDate, New = sentAt } })
            });
        }
    }
}
