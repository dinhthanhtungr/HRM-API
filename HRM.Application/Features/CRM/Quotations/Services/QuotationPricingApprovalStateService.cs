using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Đồng bộ trạng thái duyệt giá của báo giá với các bảng giá sản phẩm đã được duyệt.
/// </summary>
internal sealed class QuotationPricingApprovalStateService
{
    private const int ReconciliationBatchSize = 100;

    private readonly ICRMWriteDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly QuotationFeatureOptions _featureOptions;
    private readonly KeyedMutationLock<Guid> _mutationLock;

    public QuotationPricingApprovalStateService(
        ICRMWriteDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        QuotationFeatureOptions featureOptions,
        KeyedMutationLock<Guid> mutationLock)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _featureOptions = featureOptions;
        _mutationLock = mutationLock;
    }

    public async Task<QuotationPricingApprovalStateResult> ReconcileLockedAsync(
        Guid quotationId,
        Guid companyId,
        Guid? changedBy,
        CancellationToken cancellationToken)
    {
        var quotation = await _dbContext.Quotations
            .AsTracking()
            .Include(x => x.Lines.Where(line => line.IsActive))
                .ThenInclude(x => x.PriceTiers)
            .FirstOrDefaultAsync(x =>
                x.QuotationId == quotationId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);

        if (quotation is null)
        {
            return new QuotationPricingApprovalStateResult(
                false, QuotationStatus.Draft, null, "Quotation was not found.");
        }

        if (quotation.Status != QuotationStatus.PendingApproval)
        {
            return new QuotationPricingApprovalStateResult(
                false, quotation.Status, quotation.UpdatedDate, null);
        }

        var activeLines = quotation.Lines.Where(x => x.IsActive).ToArray();
        if (activeLines.Length == 0)
        {
            return new QuotationPricingApprovalStateResult(
                false, quotation.Status, quotation.UpdatedDate,
                "Quotation has no active lines.");
        }

        var now = _dateTimeProvider.Now;
        var productIds = activeLines.Select(x => x.ProductId).Distinct().ToArray();
        var approvedVersions = await _dbContext.ProductPricingVersions
            .AsNoTracking()
            .Include(x => x.PriceTiers)
            .Where(x =>
                x.CompanyId == companyId &&
                productIds.Contains(x.ProductId) &&
                x.Currency == quotation.Currency &&
                x.Status == ProductPricingStatus.Approved &&
                x.IsActive &&
                x.StandardSellingPrice > 0m)
            .OrderByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        var validVersionByProduct = approvedVersions
            .Where(x => IsStillValid(x, now) && HasCompleteTiers(x))
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());

        if (activeLines.Any(line => !validVersionByProduct.ContainsKey(line.ProductId)))
        {
            return new QuotationPricingApprovalStateResult(
                false, quotation.Status, quotation.UpdatedDate,
                "One or more quotation lines do not have valid approved pricing.");
        }

        var linePricing = new Dictionary<Guid, ResolvedQuotationLinePricing>(activeLines.Length);
        foreach (var line in activeLines)
        {
            var approvedVersion = validVersionByProduct[line.ProductId];
            var currentPricingResult = QuotationPriceTierBuilder.Build(
                line.QuotationLineId,
                line.Quantity,
                line.PriceTiers
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new QuotationLinePriceTierRequest
                    {
                        QuantityRangeLabel = x.QuantityRangeLabel,
                        MinQuantity = x.MinQuantity,
                        MaxQuantity = x.MaxQuantity,
                        MinInclusive = x.MinInclusive,
                        MaxInclusive = x.MaxInclusive,
                        UnitPrice = x.UnitPrice,
                        CommissionAmount = x.CommissionAmount,
                        SortOrder = x.SortOrder,
                        IsActive = x.IsActive
                    })
                    .ToArray(),
                $"quotationLines[{line.QuotationLineId}]");
            if (currentPricingResult.Success && currentPricingResult.Data is not null)
            {
                linePricing.Add(
                    line.QuotationLineId,
                    new ResolvedQuotationLinePricing(
                        approvedVersion.StandardSellingPrice!.Value,
                        null));
                continue;
            }

            var snapshotResult = QuotationPricingSnapshotFactory.Create(
                line.QuotationLineId,
                companyId,
                line.ProductId,
                quotation.Currency,
                line.Quantity,
                approvedVersion,
                $"quotationLines[{line.QuotationLineId}]");
            if (!snapshotResult.Success || snapshotResult.Data is null)
            {
                return new QuotationPricingApprovalStateResult(
                    false, quotation.Status, quotation.UpdatedDate, snapshotResult.Message);
            }

            linePricing.Add(
                line.QuotationLineId,
                new ResolvedQuotationLinePricing(
                    snapshotResult.Data.EffectiveUnitPrice,
                    snapshotResult.Data.PriceTiers));
        }

        foreach (var line in activeLines)
        {
            var approvedVersion = validVersionByProduct[line.ProductId];
            var pricing = linePricing[line.QuotationLineId];
            if (pricing.ReplacementTiers is not null)
            {
                _dbContext.QuotationLinePriceTiers.RemoveRange(line.PriceTiers);
                line.PriceTiers.Clear();
                foreach (var tier in pricing.ReplacementTiers)
                {
                    line.PriceTiers.Add(tier);
                }

                _dbContext.QuotationLinePriceTiers.AddRange(pricing.ReplacementTiers);
            }

            line.ProductPricingVersionId = approvedVersion.ProductPricingVersionId;
            line.UnitPrice = pricing.EffectiveUnitPrice;
            line.LineTotal = QuotationRules.CalculateLineTotal(
                line.Quantity, line.UnitPrice, line.DiscountPercent);
        }

        var actorId = changedBy ?? approvedVersions
            .Select(x => x.ApprovedBy ?? x.UpdatedBy ?? x.CreatedBy)
            .First();
        QuotationRules.RecalculateTotals(quotation);
        quotation.Status = QuotationStatus.Approved;
        quotation.UpdatedBy = actorId;
        quotation.UpdatedDate = now;
        _dbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(),
            QuotationId = quotation.QuotationId,
            FromStatus = QuotationStatus.PendingApproval,
            ToStatus = QuotationStatus.Approved,
            Note = "Tất cả sản phẩm đã có giá chuẩn còn hiệu lực.",
            ChangedBy = actorId,
            ChangedDate = now
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return new QuotationPricingApprovalStateResult(
                false,
                QuotationStatus.PendingApproval,
                quotation.UpdatedDate,
                OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
        }

        return new QuotationPricingApprovalStateResult(
            true, quotation.Status, quotation.UpdatedDate, null);
    }

    public async Task<IReadOnlyDictionary<Guid, QuotationPricingApprovalStateResult>>
        ReconcileForProductAsync(
            Guid productId,
            Guid companyId,
            Guid changedBy,
            CancellationToken cancellationToken)
    {
        var quotationIds = await _dbContext.QuotationLines
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.ProductId == productId &&
                x.Quotation.CompanyId == companyId &&
                x.Quotation.IsActive &&
                x.Quotation.Status == QuotationStatus.PendingApproval)
            .Select(x => x.QuotationId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var results = new Dictionary<Guid, QuotationPricingApprovalStateResult>();
        foreach (var quotationId in quotationIds)
        {
            using var lease = await _mutationLock.AcquireAsync(quotationId, cancellationToken);
            results[quotationId] = await ReconcileLockedAsync(
                quotationId, companyId, changedBy, cancellationToken);
        }

        return results;
    }

    public async Task<int> ReconcilePendingAsync(CancellationToken cancellationToken)
    {
        var targets = await _dbContext.Quotations
            .AsNoTracking()
            .Where(x => x.IsActive && x.Status == QuotationStatus.PendingApproval)
            .OrderBy(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(x => new { x.QuotationId, x.CompanyId })
            .Take(ReconciliationBatchSize)
            .ToArrayAsync(cancellationToken);

        var changedCount = 0;
        foreach (var target in targets)
        {
            using var lease = await _mutationLock.AcquireAsync(
                target.QuotationId, cancellationToken);
            var result = await ReconcileLockedAsync(
                target.QuotationId, target.CompanyId, null, cancellationToken);
            if (result.Changed)
            {
                changedCount++;
            }
        }

        return changedCount;
    }

    private bool IsStillValid(ProductPricingVersion version, DateTime now)
    {
        if (_featureOptions.ApprovedPricingReviewAfterDays <= 0)
        {
            return true;
        }

        var approvedAt = version.ApprovedAt ?? version.UpdatedDate ?? version.CreatedDate;
        return approvedAt.AddDays(_featureOptions.ApprovedPricingReviewAfterDays) > now;
    }

    private static bool HasCompleteTiers(ProductPricingVersion version)
    {
        var activeTiers = version.PriceTiers.Where(x => x.IsActive).ToArray();
        return activeTiers.Length > 0 && activeTiers.All(x => x.UnitPrice >= 0m);
    }

    private sealed record ResolvedQuotationLinePricing(
        decimal EffectiveUnitPrice,
        IReadOnlyList<QuotationLinePriceTier>? ReplacementTiers);
}

internal sealed record QuotationPricingApprovalStateResult(
    bool Changed,
    QuotationStatus Status,
    DateTime? UpdatedDate,
    string? Message);
