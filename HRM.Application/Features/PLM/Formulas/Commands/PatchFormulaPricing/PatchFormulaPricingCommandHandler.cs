using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Commands.PatchFormulaPricing;

/// <summary>
/// Cập nhật các giá snapshot của Formula. Chi phí NVL realtime không được ghi qua command này.
/// </summary>
internal sealed class PatchFormulaPricingCommandHandler
    : IRequestHandler<PatchFormulaPricingCommand, OperationResult<FormulaPricingResultDto>>
{
    private const decimal MaxSupportedPrice = 99_999_999_999_999.99m;
    private const decimal MaxProfitMarginRate = 100m;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;

    public PatchFormulaPricingCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IMaterialPriceQueryService materialPriceQueryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _materialPriceQueryService = materialPriceQueryService;
    }

    public async Task<OperationResult<FormulaPricingResultDto>> Handle(
        PatchFormulaPricingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FormulaId == Guid.Empty)
        {
            return OperationResult<FormulaPricingResultDto>.Fail("FormulaId is required.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<FormulaPricingResultDto>.Fail(
                "Current user does not have a company context.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<FormulaPricingResultDto>.Fail(
                "Current user does not have an employee profile.");
        }

        var validationError = ValidateRequest(command.Request);
        if (validationError is not null)
        {
            return OperationResult<FormulaPricingResultDto>.Fail(validationError);
        }

        var formula = await _dbContext.Formulas
            .Include(x => x.Product)
            .Where(x =>
                x.FormulaId == command.FormulaId &&
                x.IsActive &&
                x.CompanyId == companyId &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (formula is null)
        {
            return OperationResult<FormulaPricingResultDto>.Fail(
                "Formula was not found or is not accessible.");
        }

        if (FormulaConcurrencyRules.HasExpectedUpdatedDateConflictWithDatabasePrecision(
                command.Request.ExpectedUpdatedDate,
                formula.UpdatedDate))
        {
            return OperationResult<FormulaPricingResultDto>.Fail(
                "Formula pricing has changed. Reload the latest values before updating.");
        }

        var realtimeMaterialCost = await LoadRealtimeMaterialCostAsync(
            formula.FormulaId,
            companyId,
            cancellationToken);

        var calculationMaterialCost = realtimeMaterialCost.MaterialCost.GetValueOrDefault();
        var manufacturingCost = RoundIfProvided(command.Request.ManufacturingCost);
        var standardSellingPrice = RoundIfProvided(command.Request.StandardSellingPrice);
        var profitMarginRate = RoundRateIfProvided(command.Request.ProfitMarginRate);

        var changed = false;

        if (manufacturingCost.HasValue)
        {
            changed |= PatchHelper.SetNullable(
                manufacturingCost,
                () => formula.ProductionPrice,
                value => formula.ProductionPrice = value);
        }

        if (profitMarginRate.HasValue &&
            (!realtimeMaterialCost.IsComplete || !realtimeMaterialCost.MaterialCost.HasValue))
        {
            var reason = realtimeMaterialCost.MissingPriceCount == 0
                ? "the formula does not have any active items"
                : $"{realtimeMaterialCost.MissingPriceCount} formula item(s) do not have a latest price";
            return OperationResult<FormulaPricingResultDto>.Fail(
                $"Cannot calculate StandardSellingPrice because {reason}.");
        }

        if (standardSellingPrice.HasValue)
        {
            changed |= PatchHelper.SetNullable(
                standardSellingPrice,
                () => formula.PresidentPrice,
                value => formula.PresidentPrice = value);
        }
        else if (profitMarginRate.HasValue)
        {
            var calculatedStandardSellingPrice =
                FormulaPriceCalculator.CalculateStandardSellingPrice(
                    formula.Product.ColourCode ?? formula.Product.Code,
                    formula.Product.Additive,
                    calculationMaterialCost,
                    formula.ProductionPrice,
                    profitMarginRate.Value);
            if (calculatedStandardSellingPrice > MaxSupportedPrice)
            {
                return OperationResult<FormulaPricingResultDto>.Fail(
                    "The calculated StandardSellingPrice exceeds the supported price range.");
            }

            changed |= PatchHelper.SetNullable(
                RoundForStorage(calculatedStandardSellingPrice),
                () => formula.PresidentPrice,
                value => formula.PresidentPrice = value);
        }

        if (!changed)
        {
            return OperationResult<FormulaPricingResultDto>.Fail("No pricing value changed.");
        }

        var now = FormulaConcurrencyRules.NormalizeDatabaseTimestamp(_dateTimeProvider.Now);
        formula.UpdatedDate = now;
        formula.UpdatedBy = employeeId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var pricing = realtimeMaterialCost.IsComplete &&
                      realtimeMaterialCost.MaterialCost.HasValue
            ? FormulaPriceCalculator.Calculate(
                formula.Product.ColourCode ?? formula.Product.Code,
                formula.Product.Additive,
                calculationMaterialCost,
                formula.ProductionPrice,
                formula.PresidentPrice)
            : null;
        var manufacturingCostResult = pricing?.ManufacturingCost ??
            FormulaPriceCalculator.ResolveManufacturingCost(
                formula.Product.ColourCode ?? formula.Product.Code,
                formula.Product.Additive,
                formula.ProductionPrice);

        return OperationResult<FormulaPricingResultDto>.Ok(new FormulaPricingResultDto
        {
            FormulaId = formula.FormulaId,
            FormulaExternalId = formula.ExternalId,
            MaterialCost = formula.TotalPrice,
            RealtimeMaterialCost = realtimeMaterialCost.MaterialCost,
            IsRealtimeMaterialCostComplete = realtimeMaterialCost.IsComplete,
            MissingMaterialPriceCount = realtimeMaterialCost.MissingPriceCount,
            ManufacturingCost = manufacturingCostResult,
            StandardSellingPrice =
                pricing?.StandardSellingPrice ?? formula.PresidentPrice,
            ProfitMarginRate = pricing?.ProfitMarginRate,
            Pricing = pricing,
            PricingUpdatedDate = now,
            UpdatedByEmployeeId = employeeId
        });
    }

    private async Task<FormulaRealtimeMaterialCostResult> LoadRealtimeMaterialCostAsync(
        Guid formulaId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == formulaId &&
                x.IsActive &&
                x.Formula.IsActive &&
                x.Formula.CompanyId == companyId)
            .Select(x => new
            {
                ItemId = x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure
                    ? x.Material != null && x.Material.CompanyId == companyId
                        ? x.MaterialId
                        : null
                    : x.Product != null && x.Product.CompanyId == companyId
                        ? x.ProductId
                        : null,
                ItemType = x.itemType,
                x.Quantity
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new FormulaMaterialCostItem(
                x.ItemId,
                x.ItemType,
                x.Quantity))
            .ToList();

        var priceRequests = items
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => new PriceItemRequest
            {
                ItemType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(x.ItemType),
                MaterialId = IsMaterial(x.ItemType) ? x.ItemId : null,
                ProductId = IsMaterial(x.ItemType) ? null : x.ItemId
            })
            .ToList();

        var latestPriceByItem = priceRequests.Count == 0
            ? new Dictionary<PriceItemKey, LatestItemPriceDto>()
            : await _materialPriceQueryService
                .LoadLatestItemPriceInfoDictAsync(priceRequests, cancellationToken);

        return FormulaRealtimeMaterialCostCalculator.Calculate(items, latestPriceByItem);
    }

    private static bool IsMaterial(ItemType itemType)
        => itemType is ItemType.Material or ItemType.MaterialFailure;

    private static string? ValidateRequest(PatchFormulaPricingRequest request)
    {
        if (!request.ManufacturingCost.HasValue &&
            !request.StandardSellingPrice.HasValue &&
            !request.ProfitMarginRate.HasValue)
        {
            return "At least one pricing field is required.";
        }

        if (request.StandardSellingPrice.HasValue &&
            request.ProfitMarginRate.HasValue)
        {
            return "Send either StandardSellingPrice or ProfitMarginRate, not both.";
        }

        return ValidatePrice(request.ManufacturingCost, nameof(request.ManufacturingCost))
            ?? ValidatePrice(request.StandardSellingPrice, nameof(request.StandardSellingPrice))
            ?? ValidateProfitMarginRate(request.ProfitMarginRate);
    }

    private static string? ValidatePrice(decimal? value, string fieldName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value < 0m)
        {
            return $"{fieldName} cannot be negative.";
        }

        return value.Value > MaxSupportedPrice
            ? $"{fieldName} cannot exceed {MaxSupportedPrice}."
            : null;
    }

    private static decimal? RoundIfProvided(decimal? value)
    {
        return value.HasValue
            ? RoundForStorage(value.Value)
            : null;
    }

    private static decimal? RoundRateIfProvided(decimal? value)
    {
        return value.HasValue
            ? decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string? ValidateProfitMarginRate(decimal? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value is < 0m or > MaxProfitMarginRate
            ? $"ProfitMarginRate must be between 0 and {MaxProfitMarginRate}."
            : null;
    }

    private static decimal RoundForStorage(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

}
