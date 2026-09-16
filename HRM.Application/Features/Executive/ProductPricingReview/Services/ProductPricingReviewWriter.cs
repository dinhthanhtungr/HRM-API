using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Executive.ProductPricingReview.Services;

internal sealed class ProductPricingReviewWriter
{
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly KeyedMutationLock<Guid> _mutationLock;
    private readonly ProductPricingReviewCalculator _calculator;
    private readonly ProductPricingApprovalNotificationService _notificationService;
    private readonly QuotationPricingApprovalStateService _quotationStateService;

    public ProductPricingReviewWriter(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        KeyedMutationLock<Guid> mutationLock,
        ProductPricingReviewCalculator calculator,
        ProductPricingApprovalNotificationService notificationService,
        QuotationPricingApprovalStateService quotationStateService)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _currentUser = currentUser;
        _clock = clock;
        _mutationLock = mutationLock;
        _calculator = calculator;
        _notificationService = notificationService;
        _quotationStateService = quotationStateService;
    }

    public async Task<OperationResult<PricingReviewVersionDto>> CreateAsync(
        Guid productId,
        CreatePricingReviewVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PricingReviewVersionDto>.Fail(context.Message!);
        if (!Guid.TryParse(request.IdempotencyKey, out var versionId) || versionId == Guid.Empty)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "IdempotencyKey must be a non-empty GUID.");

        var companyId = context.Data.CompanyId;
        var existing = await LoadVersionAsync(versionId, companyId, cancellationToken);
        if (existing is not null)
        {
            return existing.ProductId == productId
                ? OperationResult<PricingReviewVersionDto>.Ok(ProductPricingReviewReader.MapVersion(existing))
                : OperationResult<PricingReviewVersionDto>.Fail(
                    "IdempotencyKey is already used for another product.");
        }

        var calculation = await _calculator.CalculateAsync(productId, request, cancellationToken);
        if (!calculation.Success || calculation.Data is null)
            return OperationResult<PricingReviewVersionDto>.Fail(calculation.Message!);
        if (request.SourceVersionNumber.HasValue &&
            request.SourceVersionNumber != calculation.Data.SourceVersionNumber)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "The selected source version changed. Reload source options before saving.");
        if (request.ApproveImmediately && calculation.Data.Preview.PriceTiers.Count == 0)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "At least one valid price tier is required before approval.");

        using var lease = await _mutationLock.AcquireAsync(productId, cancellationToken);
        existing = await LoadVersionAsync(versionId, companyId, cancellationToken);
        if (existing is not null)
        {
            return existing.ProductId == productId
                ? OperationResult<PricingReviewVersionDto>.Ok(ProductPricingReviewReader.MapVersion(existing))
                : OperationResult<PricingReviewVersionDto>.Fail(
                    "IdempotencyKey is already used for another product.");
        }
        var current = await _writeDbContext.ProductPricingVersions.AsTracking()
            .Where(x => x.CompanyId == companyId && x.ProductId == productId && x.Currency == "VND" &&
                        x.IsActive && (x.Status == ProductPricingStatus.Draft ||
                                       x.Status == ProductPricingStatus.Approved))
            .OrderBy(x => x.Status == ProductPricingStatus.Draft ? 0 : 1)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var concurrencyError = ValidateExpectedCurrent(request, current);
        if (concurrencyError is not null)
            return OperationResult<PricingReviewVersionDto>.Fail(concurrencyError);

        var latestVersion = await _writeDbContext.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ProductId == productId && x.Currency == "VND")
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;
        var now = _clock.Now;
        if (request.ApproveImmediately)
        {
            var previous = await _writeDbContext.ProductPricingVersions.AsTracking()
                .Where(x => x.CompanyId == companyId && x.ProductId == productId && x.Currency == "VND" &&
                            x.IsActive && (x.Status == ProductPricingStatus.Draft ||
                                           x.Status == ProductPricingStatus.Approved))
                .ToListAsync(cancellationToken);
            foreach (var item in previous)
            {
                item.Status = item.Status == ProductPricingStatus.Approved
                    ? ProductPricingStatus.Superseded
                    : ProductPricingStatus.Cancelled;
                item.UpdatedBy = context.Data.EmployeeId;
                item.UpdatedDate = now;
            }
        }

        var entity = BuildVersion(
            versionId,
            productId,
            companyId,
            context.Data.EmployeeId,
            latestVersion + 1,
            request.PublisherNote,
            request.InternalNote,
            request.ApproveImmediately,
            request.PriceTiers?.Count > 0,
            calculation.Data,
            now);
        await _writeDbContext.ProductPricingVersions.AddAsync(entity, cancellationToken);
        var save = await SaveAsync(cancellationToken);
        if (save is not null)
            return OperationResult<PricingReviewVersionDto>.Fail(save);

        var saved = await LoadVersionAsync(versionId, companyId, cancellationToken);
        if (saved is null)
            return OperationResult<PricingReviewVersionDto>.Fail("Saved pricing version could not be reloaded.");
        if (request.ApproveImmediately)
            await PublishApprovalAsync(saved, companyId, context.Data.EmployeeId, cancellationToken);
        return OperationResult<PricingReviewVersionDto>.Ok(
            ProductPricingReviewReader.MapVersion(saved),
            request.ApproveImmediately
                ? "Pricing version created and approved successfully."
                : "Pricing draft created successfully.");
    }

    public async Task<OperationResult<PricingReviewVersionDto>> UpdateAsync(
        Guid productId,
        Guid versionId,
        UpdatePricingReviewVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PricingReviewVersionDto>.Fail(context.Message!);
        using var lease = await _mutationLock.AcquireAsync(productId, cancellationToken);
        var entity = await _writeDbContext.ProductPricingVersions.AsTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.CreatedByNavigation)
            .Include(x => x.UpdatedByNavigation)
            .FirstOrDefaultAsync(x => x.ProductPricingVersionId == versionId &&
                x.ProductId == productId && x.CompanyId == context.Data.CompanyId && x.IsActive,
                cancellationToken);
        if (entity is null)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "Pricing version was not found or is outside the current company/product.");
        if (entity.Status != ProductPricingStatus.Draft)
            return OperationResult<PricingReviewVersionDto>.Fail("Only a Draft pricing version can be updated.");
        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            request.ExpectedUpdatedAt, entity.UpdatedDate, "Product pricing version");
        if (concurrencyError is not null)
            return OperationResult<PricingReviewVersionDto>.Fail(concurrencyError);

        var calculation = await _calculator.CalculateAsync(productId, request, cancellationToken);
        if (!calculation.Success || calculation.Data is null)
            return OperationResult<PricingReviewVersionDto>.Fail(calculation.Message!);
        if (request.SourceVersionNumber.HasValue &&
            request.SourceVersionNumber != calculation.Data.SourceVersionNumber)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "The selected source version changed. Reload source options before saving.");

        _writeDbContext.ProductPricingTiers.RemoveRange(entity.PriceTiers);
        entity.PriceTiers.Clear();
        foreach (var tier in BuildTiers(entity.ProductPricingVersionId, calculation.Data.Preview.PriceTiers))
            entity.PriceTiers.Add(tier);
        ApplyCalculation(entity, calculation.Data);
        entity.HasManualTierAdjustment = request.PriceTiers?.Count > 0;
        entity.PublisherNote = NormalizeNote(request.PublisherNote);
        entity.Note = NormalizeNote(request.InternalNote);
        entity.UpdatedBy = context.Data.EmployeeId;
        entity.UpdatedDate = _clock.Now;
        entity.CalculatedAt = entity.UpdatedDate;
        var save = await SaveAsync(cancellationToken);
        return save is not null
            ? OperationResult<PricingReviewVersionDto>.Fail(save)
            : OperationResult<PricingReviewVersionDto>.Ok(
                ProductPricingReviewReader.MapVersion(entity),
                "Pricing draft updated successfully.");
    }

    public async Task<OperationResult<PricingReviewVersionDto>> ApproveAsync(
        Guid productId,
        Guid versionId,
        ApprovePricingReviewVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PricingReviewVersionDto>.Fail(context.Message!);
        using var lease = await _mutationLock.AcquireAsync(productId, cancellationToken);
        var entity = await _writeDbContext.ProductPricingVersions.AsTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.CreatedByNavigation)
            .Include(x => x.UpdatedByNavigation)
            .FirstOrDefaultAsync(x => x.ProductPricingVersionId == versionId &&
                x.ProductId == productId && x.CompanyId == context.Data.CompanyId && x.IsActive,
                cancellationToken);
        if (entity is null)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "Pricing version was not found or is outside the current company/product.");
        if (entity.Status != ProductPricingStatus.Draft)
            return OperationResult<PricingReviewVersionDto>.Fail("Only a Draft pricing version can be approved.");
        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            request.ExpectedUpdatedAt, entity.UpdatedDate, "Product pricing version");
        if (concurrencyError is not null)
            return OperationResult<PricingReviewVersionDto>.Fail(concurrencyError);

        var sourceId = entity.SourceManufacturingFormulaId ?? entity.SourceFormulaId;
        if (!sourceId.HasValue)
            return OperationResult<PricingReviewVersionDto>.Fail("Draft pricing source is missing.");
        var previewRequest = new PricingReviewPreviewRequest
        {
            Currency = entity.Currency,
            SourceType = entity.SourceManufacturingFormulaId.HasValue
                ? PricingReviewSourceType.VA
                : PricingReviewSourceType.VU,
            SourceId = sourceId.Value,
            ManufacturingCost = entity.ManufacturingCost,
            StandardSellingPrice = entity.StandardSellingPrice,
            ProfitMarginPercent = PricingMarginCalculator.CalculateProfitMarginPercent(
                entity.StandardSellingPrice,
                entity.MaterialCostSnapshot + entity.ManufacturingCost),
            ChangedField = PricingReviewChangedField.StandardSellingPrice,
            PriceTiers = entity.PriceTiers.OrderBy(x => x.SortOrder).Select(x => new PricingReviewPriceTierRequest
            {
                QuantityRangeLabel = x.QuantityRangeLabel,
                MinQuantity = x.MinQuantity,
                MaxQuantity = x.MaxQuantity,
                MinInclusive = x.MinInclusive,
                MaxInclusive = x.MaxInclusive,
                UnitPrice = x.UnitPrice,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            }).ToArray()
        };
        var calculation = await _calculator.CalculateAsync(productId, previewRequest, cancellationToken);
        if (!calculation.Success || calculation.Data is null)
            return OperationResult<PricingReviewVersionDto>.Fail(calculation.Message!);
        if (entity.FormulaPricingPolicyId != calculation.Data.FormulaPricingPolicyId)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "The pricing policy changed. Create a new Draft before approval. " +
                OptimisticConcurrencyHelper.ConflictMessageMarker);
        if (calculation.Data.Preview.StandardSellingPrice <= 0m ||
            calculation.Data.Preview.PriceTiers.Count == 0)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "A positive standard selling price and at least one price tier are required before approval.");

        var now = _clock.Now;
        var previousApproved = await _writeDbContext.ProductPricingVersions.AsTracking()
            .Where(x => x.ProductPricingVersionId != versionId && x.CompanyId == context.Data.CompanyId &&
                        x.ProductId == productId && x.Currency == entity.Currency && x.IsActive &&
                        x.Status == ProductPricingStatus.Approved)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousApproved)
        {
            previous.Status = ProductPricingStatus.Superseded;
            previous.UpdatedBy = context.Data.EmployeeId;
            previous.UpdatedDate = now;
        }

        ApplyCalculation(entity, calculation.Data);
        if (request.PublisherNote is not null)
            entity.PublisherNote = NormalizeNote(request.PublisherNote);
        if (request.InternalNote is not null)
            entity.Note = NormalizeNote(request.InternalNote);
        entity.Status = ProductPricingStatus.Approved;
        entity.ApprovedBy = context.Data.EmployeeId;
        entity.ApprovedAt = now;
        entity.UpdatedBy = context.Data.EmployeeId;
        entity.UpdatedDate = now;
        entity.CalculatedAt = now;
        var save = await SaveAsync(cancellationToken);
        if (save is not null)
            return OperationResult<PricingReviewVersionDto>.Fail(save);

        var saved = await LoadVersionAsync(versionId, context.Data.CompanyId, cancellationToken) ?? entity;
        await PublishApprovalAsync(saved, context.Data.CompanyId, context.Data.EmployeeId, cancellationToken);
        return OperationResult<PricingReviewVersionDto>.Ok(
            ProductPricingReviewReader.MapVersion(saved),
            "Pricing version approved successfully.");
    }

    /// <summary>
    /// Reconfirms the already-published standard price without changing its
    /// source or values. A new Approved version is intentionally written so the
    /// confirmation has an auditable timestamp and closes any derived pending
    /// reapproval state without schema changes.
    /// </summary>
    public async Task<OperationResult<PricingReviewVersionDto>> ConfirmCurrentAsync(
        Guid productId,
        ConfirmCurrentStandardPriceRequest request,
        CancellationToken cancellationToken)
    {
        var context = ProductPricingReviewRules.GetContext(_currentUser);
        if (!context.Success)
            return OperationResult<PricingReviewVersionDto>.Fail(context.Message!);
        if (!Guid.TryParse(request.IdempotencyKey, out var versionId) || versionId == Guid.Empty)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "IdempotencyKey must be a non-empty GUID.");

        using var lease = await _mutationLock.AcquireAsync(productId, cancellationToken);
        var existing = await LoadVersionAsync(versionId, context.Data.CompanyId, cancellationToken);
        if (existing is not null)
        {
            return existing.ProductId == productId
                ? OperationResult<PricingReviewVersionDto>.Ok(ProductPricingReviewReader.MapVersion(existing))
                : OperationResult<PricingReviewVersionDto>.Fail(
                    "IdempotencyKey is already used for another product.");
        }

        var approved = await _writeDbContext.ProductPricingVersions.AsTracking()
            .Include(x => x.PriceTiers)
            .Where(x =>
                x.CompanyId == context.Data.CompanyId &&
                x.ProductId == productId &&
                x.Currency == "VND" &&
                x.IsActive &&
                x.Status == ProductPricingStatus.Approved &&
                x.StandardSellingPrice > 0m)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (approved is null)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "There is no approved standard price to confirm.");
        if (request.ExpectedApprovedPricingVersionId.HasValue &&
            request.ExpectedApprovedPricingVersionId.Value != approved.ProductPricingVersionId)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "Approved standard price changed. Reload before confirming. " +
                OptimisticConcurrencyHelper.ConflictMessageMarker);
        var concurrencyError = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            request.ExpectedApprovedPricingVersionUpdatedAt,
            approved.UpdatedDate,
            "Approved product pricing version");
        if (concurrencyError is not null)
            return OperationResult<PricingReviewVersionDto>.Fail(concurrencyError);

        var hasDraft = await _writeDbContext.ProductPricingVersions.AsNoTracking().AnyAsync(x =>
            x.CompanyId == context.Data.CompanyId && x.ProductId == productId && x.Currency == "VND" &&
            x.IsActive && x.Status == ProductPricingStatus.Draft, cancellationToken);
        if (hasDraft)
            return OperationResult<PricingReviewVersionDto>.Fail(
                "A pricing Draft is in progress. Approve or resolve that Draft before confirming the current price.");

        var latestVersion = await _writeDbContext.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == context.Data.CompanyId && x.ProductId == productId && x.Currency == "VND")
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;
        var now = _clock.Now;
        approved.Status = ProductPricingStatus.Superseded;
        approved.UpdatedBy = context.Data.EmployeeId;
        approved.UpdatedDate = now;

        var confirmed = CloneApprovedVersion(
            approved,
            versionId,
            latestVersion + 1,
            context.Data.EmployeeId,
            now,
            request.InternalNote);
        await _writeDbContext.ProductPricingVersions.AddAsync(confirmed, cancellationToken);
        var save = await SaveAsync(cancellationToken);
        if (save is not null)
            return OperationResult<PricingReviewVersionDto>.Fail(save);

        // Reconciliation keeps quotation read models consistent. Do not publish a
        // second price-change notification: the values did not change.
        await _quotationStateService.ReconcileForProductAsync(
            productId, context.Data.CompanyId, context.Data.EmployeeId, cancellationToken);
        var saved = await LoadVersionAsync(versionId, context.Data.CompanyId, cancellationToken) ?? confirmed;
        return OperationResult<PricingReviewVersionDto>.Ok(
            ProductPricingReviewReader.MapVersion(saved),
            "Current standard price confirmed successfully.");
    }

    private static ProductPricingVersion BuildVersion(
        Guid id, Guid productId, Guid companyId, Guid employeeId, int version,
        string? publisherNote, string? note, bool approved, bool hasManualTiers,
        PricingReviewCalculation calculation, DateTime now)
    {
        var entity = new ProductPricingVersion
        {
            ProductPricingVersionId = id,
            ProductId = productId,
            CompanyId = companyId,
            Currency = "VND",
            FormulaPricingPolicyId = calculation.FormulaPricingPolicyId,
            SourceFormulaId = calculation.SourceType == ProductPricingSourceType.Formula
                ? calculation.Preview.Source.SourceId : null,
            SourceManufacturingFormulaId = calculation.SourceType == ProductPricingSourceType.ManufacturingFormula
                ? calculation.Preview.Source.SourceId : null,
            FormulaExternalIdSnapshot = calculation.SourceExternalId,
            MaterialCostSnapshot = calculation.Preview.MaterialCost,
            ManufacturingCost = calculation.Preview.ManufacturingCost,
            StandardSellingPrice = calculation.Preview.StandardSellingPrice,
            ProfitMarginRate = calculation.ProfitMarginRate,
            PublisherNote = NormalizeNote(publisherNote),
            Note = NormalizeNote(note),
            Status = approved ? ProductPricingStatus.Approved : ProductPricingStatus.Draft,
            Version = version,
            HasManualTierAdjustment = hasManualTiers,
            CalculatedAt = now,
            IsActive = true,
            CreatedBy = employeeId,
            CreatedDate = now,
            UpdatedBy = employeeId,
            UpdatedDate = now,
            ApprovedBy = approved ? employeeId : null,
            ApprovedAt = approved ? now : null
        };
        entity.PriceTiers = BuildTiers(id, calculation.Preview.PriceTiers).ToList();
        return entity;
    }

    private static ProductPricingVersion CloneApprovedVersion(
        ProductPricingVersion source,
        Guid versionId,
        int version,
        Guid employeeId,
        DateTime now,
        string? internalNote)
    {
        var copy = new ProductPricingVersion
        {
            ProductPricingVersionId = versionId,
            CompanyId = source.CompanyId,
            ProductId = source.ProductId,
            FormulaPricingPolicyId = source.FormulaPricingPolicyId,
            HasManualTierAdjustment = source.HasManualTierAdjustment,
            SourceFormulaId = source.SourceFormulaId,
            SourceManufacturingFormulaId = source.SourceManufacturingFormulaId,
            SourceSampleTrialId = source.SourceSampleTrialId,
            SourceManufacturingVUFormulaId = source.SourceManufacturingVUFormulaId,
            FormulaExternalIdSnapshot = source.FormulaExternalIdSnapshot,
            BatchNoSnapshot = source.BatchNoSnapshot,
            Currency = source.Currency,
            MaterialCostSnapshot = source.MaterialCostSnapshot,
            ManufacturingCost = source.ManufacturingCost,
            StandardSellingPrice = source.StandardSellingPrice,
            ProfitMarginRate = source.ProfitMarginRate,
            PublisherNote = source.PublisherNote,
            Note = NormalizeNote(internalNote) ?? source.Note,
            Status = ProductPricingStatus.Approved,
            Version = version,
            CalculatedAt = now,
            ApprovedBy = employeeId,
            ApprovedAt = now,
            IsActive = true,
            CreatedBy = employeeId,
            CreatedDate = now,
            UpdatedBy = employeeId,
            UpdatedDate = now,
            PriceTiers = source.PriceTiers.Select(tier => new ProductPricingTier
            {
                ProductPricingTierId = Guid.CreateVersion7(),
                ProductPricingVersionId = versionId,
                QuantityRangeLabel = tier.QuantityRangeLabel,
                MinQuantity = tier.MinQuantity,
                MaxQuantity = tier.MaxQuantity,
                MinInclusive = tier.MinInclusive,
                MaxInclusive = tier.MaxInclusive,
                UnitPrice = tier.UnitPrice,
                SortOrder = tier.SortOrder,
                IsActive = tier.IsActive
            }).ToList()
        };
        return copy;
    }

    private static void ApplyCalculation(ProductPricingVersion entity, PricingReviewCalculation calculation)
    {
        entity.FormulaPricingPolicyId = calculation.FormulaPricingPolicyId;
        entity.SourceFormulaId = calculation.SourceType == ProductPricingSourceType.Formula
            ? calculation.Preview.Source.SourceId : null;
        entity.SourceManufacturingFormulaId = calculation.SourceType == ProductPricingSourceType.ManufacturingFormula
            ? calculation.Preview.Source.SourceId : null;
        entity.FormulaExternalIdSnapshot = calculation.SourceExternalId;
        entity.MaterialCostSnapshot = calculation.Preview.MaterialCost;
        entity.ManufacturingCost = calculation.Preview.ManufacturingCost;
        entity.StandardSellingPrice = calculation.Preview.StandardSellingPrice;
        entity.ProfitMarginRate = calculation.ProfitMarginRate;
    }

    private static IEnumerable<ProductPricingTier> BuildTiers(
        Guid versionId, IEnumerable<PricingReviewPriceTierDto> tiers)
        => tiers.Select(x => new ProductPricingTier
        {
            ProductPricingTierId = Guid.CreateVersion7(),
            ProductPricingVersionId = versionId,
            QuantityRangeLabel = x.QuantityRangeLabel,
            MinQuantity = x.MinQuantity,
            MaxQuantity = x.MaxQuantity,
            MinInclusive = x.MinInclusive,
            MaxInclusive = x.MaxInclusive,
            UnitPrice = x.UnitPrice ?? throw new InvalidOperationException(
                "A persisted pricing tier must have a unit price."),
            SortOrder = x.SortOrder,
            IsActive = x.IsActive
        });

    private static string? ValidateExpectedCurrent(
        CreatePricingReviewVersionRequest request, ProductPricingVersion? current)
    {
        if (request.ExpectedCurrentVersionId.HasValue &&
            request.ExpectedCurrentVersionId != current?.ProductPricingVersionId)
            return "Current pricing version changed. Reload before saving. " +
                   OptimisticConcurrencyHelper.ConflictMessageMarker;
        return OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
            request.ExpectedCurrentVersionUpdatedAt,
            current?.UpdatedDate,
            "Current product pricing version");
    }

    private async Task<ProductPricingVersion?> LoadVersionAsync(
        Guid versionId, Guid companyId, CancellationToken cancellationToken)
        => await _readDbContext.ProductPricingVersions.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.PriceTiers)
            .Include(x => x.CreatedByNavigation)
            .Include(x => x.UpdatedByNavigation)
            .FirstOrDefaultAsync(x => x.ProductPricingVersionId == versionId && x.CompanyId == companyId,
                cancellationToken);

    private async Task<string?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _writeDbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OptimisticConcurrencyHelper.CreateConflictMessage("Product pricing version", exception);
        }
        catch (DbUpdateException)
        {
            return "Product pricing version could not be saved. Reload and try again.";
        }
    }

    private async Task PublishApprovalAsync(
        ProductPricingVersion entity,
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        await _quotationStateService.ReconcileForProductAsync(
            entity.ProductId, companyId, employeeId, cancellationToken);
        await _notificationService.PublishAsync(entity, companyId, employeeId, cancellationToken);
    }

    private static string? NormalizeNote(string? note)
    {
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return normalized is { Length: > 2000 } ? normalized[..2000] : normalized;
    }
}
