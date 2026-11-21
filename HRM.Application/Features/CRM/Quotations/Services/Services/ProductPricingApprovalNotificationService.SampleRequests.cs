using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class ProductPricingApprovalNotificationService
{
    private readonly HashSet<Guid> _queuedSampleApprovals = [];
    // Called before the approval SaveChanges: approval, immutable notice and outbox are committed together.
    internal async Task QueueSampleRequestApprovalsAsync(ProductPricingVersion price, CancellationToken ct)
    {
        if (price.Status != ProductPricingStatus.Approved || price.SourceFormulaId is not { } formulaId ||
            price.ApprovedBy is not { } actor || price.ApprovedAt is not { } approvedAt || price.StandardSellingPrice is not > 0) return;
        var requests = await ApprovalTargets(price).Select(request => new {
            request.SampleRequestId, request.RequestType, CustomerCode = request.Customer.ExternalId }).ToArrayAsync(ct);
        if (requests.Length == 0) return;
        var eventIds = requests.Select(r => ApprovalEventId(price.ProductPricingVersionId, r.SampleRequestId)).ToArray();
        var existing = await _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == price.CompanyId && eventIds.Contains(e.Id)).Select(e => e.Id).ToArrayAsync(ct);
        var added = false;
        foreach (var request in requests)
        {
            if (SampleRequestMessageRules.ShouldSuppressMessages(request.RequestType, request.CustomerCode)) continue;
            var id = ApprovalEventId(price.ProductPricingVersionId, request.SampleRequestId);
            if (existing.Contains(id) || !_queuedSampleApprovals.Add(id)) continue;
            var evt = new SampleRequestWorkflowEvent { Id = id, CompanyId = price.CompanyId, SampleRequestId = request.SampleRequestId,
                FormulaId = formulaId, ProductPricingVersionId = price.ProductPricingVersionId, RecordedByEmployeeId = actor,
                EventType = SampleFlowRules.StandardPriceApproved, RecordedAt = approvedAt, OccurredAt = approvedAt };
            _dbContext.SampleRequestWorkflowEvents.Add(evt);
            SampleRequestWorkflowNotificationQueue.Queue(_dbContext.OutboxMessages, evt, new SendSampleRequestMessageCommand {
                Message = ApprovalText(price), TopicOverride = TopicNotifications.SampleRequestReferencePriceAvailable,
                TitleOverride = "BGD đã duyệt giá chuẩn"
            });
            added = true;
        }
        if (added) _dbContext.OutboxMessages.Add(new OutboxMessage { Type = SampleWorkRealtimeSignal.OutboxType,
            CreatedAt = approvedAt, PayloadJson = JsonSerializer.Serialize(new { price.CompanyId }) });
    }

    internal IQueryable<SampleRequest> ApprovalTargets(ProductPricingVersion price)
    {
        var formulaId = price.SourceFormulaId ?? Guid.Empty;
        return from request in _dbContext.SampleRequests.AsNoTracking()
                              where request.CompanyId == price.CompanyId && request.ProductId == price.ProductId && request.IsActive &&
                                  request.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId
                              let confirmed = _dbContext.SampleRequestWorkflowEvents.Where(e => e.CompanyId == price.CompanyId &&
                                  e.SampleRequestId == request.SampleRequestId && e.EventType == SampleRequestWorkflowEvents.FormulaConfirmed && e.FormulaId != null)
                                  .OrderByDescending(e => e.RecordedAt).ThenByDescending(e => e.Id).Select(e => e.FormulaId).FirstOrDefault()
                              let trial = _dbContext.SampleRequestSampleTrials.Where(t => t.SampleRequestId == request.SampleRequestId && t.IsActive)
                                  .OrderByDescending(t => t.TrialNo).FirstOrDefault()
                              let basis = (request.CreatedDate < SampleFlowRules.StartsAt ? request.FormulaId :
                                  trial != null ? (trial.Status == HRM.Domain.Enums.SampleRequests.SampleTrialStatus.Failed
                                  ? confirmed ?? trial.FormulaId : trial.FormulaId) : null) ?? confirmed ?? request.FormulaId
                              where basis == formulaId && _dbContext.Formulas.Any(f => f.FormulaId == formulaId && f.CompanyId == price.CompanyId && f.ProductId == price.ProductId && f.IsActive)
                              select request;
    }

    internal static Guid ApprovalEventId(Guid version, Guid request)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"sample-price-approved:{version:N}:{request:N}")).AsSpan(0, 16));

    internal static string ApprovalText(ProductPricingVersion price)
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var currency = price.Currency;
        var lines = new List<string> { "BGD đã duyệt giá chuẩn", $"Phiên bản giá: {price.Version}",
            $"Giá chuẩn: {price.StandardSellingPrice?.ToString("N0", culture)} {currency}/kg",
            $"Ngày duyệt: {price.ApprovedAt:dd/MM/yyyy HH:mm}" };
        var tiers = price.PriceTiers.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToArray();
        for (var i = 0; i < tiers.Length; i++)
        {
            var tier = tiers[i];
            // Bounds are persisted business data; never include cost, margin or internal notes.
            var range = $"{(tier.MinInclusive ? "[" : "(")}{tier.MinQuantity?.ToString("0.####", culture) ?? "0"}; {tier.MaxQuantity?.ToString("0.####", culture) ?? "∞"}{(tier.MaxInclusive ? "]" : ")")} kg";
            var line = $"{range}: {tier.UnitPrice.ToString("N0", culture)} {currency}/kg";
            // Reserve space for the source/approver appended by the dispatcher. Never silently cut a price tier.
            if (lines.Sum(x => x.Length + 1) + line.Length > 1300)
            {
                lines.Add($"Còn {tiers.Length - i} khoảng giá; mở tổng quan mẫu & giá để xem đầy đủ.");
                break;
            }
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }
}
