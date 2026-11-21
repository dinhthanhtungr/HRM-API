using System.Text.Json;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>Durable follow-up to a committed approval; never runs quotation sending on the approval request.</summary>
public sealed record QuotationPricingApprovalAutoSend(Guid CompanyId, Guid ProductId,
    Guid PricingVersionId, Guid ApprovedBy, DateTime ApprovedAt)
{
    public const string OutboxType = "QuotationPricingApprovalAutoSend";
    public const string ConfirmationMethod = "automatic_standard_price_approval";

    internal static OutboxMessage Create(Guid companyId, Guid productId, Guid versionId, Guid actorId, DateTime now)
        => new()
        {
            Type = OutboxType, CreatedAt = now,
            // Pricing approval columns are PostgreSQL timestamp without time zone (microsecond precision).
            PayloadJson = JsonSerializer.Serialize(new QuotationPricingApprovalAutoSend(companyId, productId, versionId, actorId,
                new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Unspecified)))
        };
}

public interface IQuotationPricingApprovalAutoSendProcessor
{
    Task ProcessAsync(QuotationPricingApprovalAutoSend approval, CancellationToken cancellationToken);
}

internal sealed class QuotationPricingApprovalAutoSendProcessor(
    ICRMReadDbContext db, QuotationSendService sender) : IQuotationPricingApprovalAutoSendProcessor
{
    public async Task ProcessAsync(QuotationPricingApprovalAutoSend approval, CancellationToken cancellationToken)
    {
        // No caller-supplied company/actor: events are written only by the authorized approval handler.
        var valid = await db.ProductPricingVersions.AsNoTracking().AnyAsync(x =>
            x.ProductPricingVersionId == approval.PricingVersionId && x.CompanyId == approval.CompanyId &&
            x.ProductId == approval.ProductId && x.ApprovedBy == approval.ApprovedBy && x.ApprovedAt == approval.ApprovedAt &&
            (x.Status == ProductPricingStatus.Approved || x.Status == ProductPricingStatus.Superseded), cancellationToken);
        if (!valid) throw new InvalidOperationException("Pricing approval event does not match its persisted approval.");

        var ids = await db.Quotations.AsNoTracking().Where(x => x.CompanyId == approval.CompanyId && x.IsActive &&
            x.Status == QuotationStatus.PendingSendConfirmation && x.Lines.Any(l => l.IsActive && l.ProductId == approval.ProductId))
            .Select(x => x.QuotationId).ToArrayAsync(cancellationToken);
        var failed = false;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { await sender.AutoSendAfterPricingApprovalAsync(id, approval, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { failed = true; } // Each quotation rolls back separately; continue the others, retry this event later.
        }
        if (failed) throw new InvalidOperationException("Some pending quotations could not be processed; the approval follow-up will retry.");
    }
}
