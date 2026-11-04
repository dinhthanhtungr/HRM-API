using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>Rebind saved references only; never replace customer tiers or commercial inputs.</summary>
internal sealed class QuotationStandardReferenceSaveService(ICRMReadDbContext db)
{
    public async Task<IReadOnlyList<QuotationStandardReferenceChange>> PrepareAsync(
        Quotation quotation, SaveQuotationRequest request, CancellationToken cancellationToken)
    {
        var saved = quotation.Lines.Where(x => x.IsActive && x.ProductPricingVersionId.HasValue).ToArray();
        if (saved.Length == 0) return [];
        var products = saved.Select(x => x.ProductId).Distinct().ToArray();
        var oldIds = saved.Select(x => x.ProductPricingVersionId!.Value)
            .Concat(request.ObservedStandardPriceVersions.Values)
            .Concat(request.Lines?.Where(x => x.ProductPricingVersionId.HasValue).Select(x => x.ProductPricingVersionId!.Value) ?? [])
            .Distinct().ToArray();
        var versions = await db.ProductPricingVersions.AsNoTracking().Include(x => x.PriceTiers)
            .Where(x => x.CompanyId == quotation.CompanyId && products.Contains(x.ProductId) &&
                (oldIds.Contains(x.ProductPricingVersionId) ||
                 (x.IsActive && x.Currency == "VND" && x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m)))
            .ToListAsync(cancellationToken);
        var changes = new List<QuotationStandardReferenceChange>();
        foreach (var line in saved)
        {
            var incoming = request.Lines?.FirstOrDefault(x => x.QuotationLineId == line.QuotationLineId && x.ProductId == line.ProductId);
            // A removed/replaced product is validated as a new line by the normal builder.
            if (request.Lines is not null && incoming is null) continue;
            if (incoming is not null && incoming.PriceMode != line.PriceMode && incoming.ProductPricingVersionId.HasValue)
                continue;
            var baselineId = incoming?.ProductPricingVersionId ?? request.ObservedStandardPriceVersions.GetValueOrDefault(line.QuotationLineId, line.ProductPricingVersionId!.Value);
            var old = versions.FirstOrDefault(x => x.ProductPricingVersionId == baselineId && x.ProductId == line.ProductId);
            if (old is null) continue; // Corrupt/foreign IDs are never accepted by the builder.
            var current = versions.Where(x => x.ProductId == line.ProductId && x.Currency == "VND" &&
                    x.IsActive && x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m)
                .OrderByDescending(x => x.Version).ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate).FirstOrDefault();
            if (current?.ProductPricingVersionId == old.ProductPricingVersionId)
            {
                line.ProductPricingVersionId = current.ProductPricingVersionId;
                continue;
            }
            var changed = current is not null && Content(old) != Content(current);
            var token = current is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{line.QuotationLineId}:{old.ProductPricingVersionId}:{current.ProductPricingVersionId}:{Content(current)}")));
            var alreadyObserved = current is not null && request.ObservedStandardPriceVersions.TryGetValue(line.QuotationLineId, out var observed) &&
                observed == current.ProductPricingVersionId;
            var accepted = !changed || alreadyObserved || (token is not null && request.AcceptedStandardPriceChanges.TryGetValue(line.QuotationLineId, out var supplied) && supplied == token);
            changes.Add(new(line.QuotationLineId, line.ProductId, line.ProductExternalIdSnapshot,
                old.ProductPricingVersionId, current?.ProductPricingVersionId, old.StandardSellingPrice,
                current?.StandardSellingPrice, old.Currency, current?.Currency,
                old.PriceTiers.OrderBy(x => x.SortOrder).Select(Tier).ToArray(),
                current?.PriceTiers.OrderBy(x => x.SortOrder).Select(Tier).ToArray() ?? [], token, current is null, changed && !accepted));
            if (!accepted) continue;
            if (current is not null) line.ProductPricingVersionId = current.ProductPricingVersionId;
            if (incoming is not null)
            {
                incoming.ProductPricingVersionId = current is null ? old.ProductPricingVersionId
                    : incoming.PriceMode == QuotationLinePriceMode.ApprovedPricingEditable ? current.ProductPricingVersionId : null;
                if (current is null) incoming.PriceMode = QuotationLinePriceMode.ManualAuthorized;
            }
        }
        return changes;
    }

    internal static string Content(ProductPricingVersion version) => JsonSerializer.Serialize(new
    {
        version.Currency, StandardSellingPrice = version.StandardSellingPrice?.ToString("G29", CultureInfo.InvariantCulture),
        Tiers = version.PriceTiers.OrderBy(x => x.MinQuantity).ThenBy(x => x.MaxQuantity)
            .ThenBy(x => x.IsActive).ThenBy(x => x.UnitPrice).Select(x => new
        {
            MinQuantity = x.MinQuantity?.ToString("G29", CultureInfo.InvariantCulture),
            MaxQuantity = x.MaxQuantity?.ToString("G29", CultureInfo.InvariantCulture), x.MinInclusive, x.MaxInclusive,
            UnitPrice = x.UnitPrice.ToString("G29", CultureInfo.InvariantCulture), x.IsActive
        })
    });
    private static QuotationStandardReferenceTier Tier(ProductPricingTier tier) => new(
        tier.QuantityRangeLabel, tier.MinQuantity, tier.MaxQuantity, tier.MinInclusive, tier.MaxInclusive, tier.UnitPrice, tier.SortOrder, tier.IsActive);
}

