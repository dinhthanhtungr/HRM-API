using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.Formulas;
using HRM.Application.Commons.Pricing.Dtos;

namespace HRM.Application.Commons.Pricing.Services;

/// <summary>
/// Shared pricing engine: resolves only effective published policy and computes a result from it.
/// It deliberately has no CRM controller or endpoint dependency.
/// </summary>
public sealed class FormulaPricingEngine(
    IFormulaPricingPolicyResolver policyResolver,
    IMaterialPriceQueryService materialPriceQueryService)
{
    public async Task<OperationResult<PricingEngineResult>> ResolveAsync(
        PricingEngineRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty || request.Profile is not { } profile ||
            !Enum.IsDefined(profile) || string.IsNullOrWhiteSpace(request.Currency))
            return OperationResult<PricingEngineResult>.Fail("PricingEngineInvalidRequest");
        if (request.MaterialCost < 0m || request.ManufacturingCostOverride < 0m ||
            request.StandardSellingPrice < 0m || request.ProfitMarginRate is < 0m or > 100m ||
            (request.ChangedField.HasValue && !Enum.IsDefined(request.ChangedField.Value)))
            return OperationResult<PricingEngineResult>.Fail("PricingEngineInvalidRange");

        var key = new FormulaPricingPolicyLookupKey(
            request.CompanyId, profile, request.Currency.Trim().ToUpperInvariant());
        var policies = await policyResolver.GetPublishedBatchAsync([key], cancellationToken);
        if (!policies.TryGetValue(key, out var policy))
            return OperationResult<PricingEngineResult>.Fail("PricingPolicyMissing");

        var realtime = await ResolveMaterialCostAsync(request, cancellationToken);
        if (!realtime.IsComplete || !realtime.MaterialCost.HasValue)
            return OperationResult<PricingEngineResult>.Ok(new PricingEngineResult
            {
                FormulaPricingPolicyId = policy.FormulaPricingPolicyId,
                FormulaPricingPolicyVersion = policy.Version,
                Profile = profile,
                Currency = key.Currency,
                ProductId = request.ProductId,
                SourceId = request.SourceId,
                SourceType = request.SourceType,
                IsMaterialCostComplete = false,
                MissingMaterialPriceCount = realtime.MissingPriceCount,
                SuggestedTiers = FormulaPriceCalculator.BuildPriceTierTemplates(policy.Definition)
            });

        FormulaPriceCalculationDto calculation;
        try
        {
            calculation = FormulaPriceCalculator.Calculate(
                policy.Definition, realtime.MaterialCost.Value,
                request.ManufacturingCostOverride, request.StandardSellingPrice,
                request.ProfitMarginRate, request.ChangedField);
        }
        catch (ArgumentOutOfRangeException)
        {
            return OperationResult<PricingEngineResult>.Fail("PricingEngineInvalidRange");
        }
        return OperationResult<PricingEngineResult>.Ok(new PricingEngineResult
        {
            FormulaPricingPolicyId = policy.FormulaPricingPolicyId,
            FormulaPricingPolicyVersion = policy.Version,
            Profile = profile,
            Currency = key.Currency,
            ProductId = request.ProductId,
            SourceId = request.SourceId,
            SourceType = request.SourceType,
            IsMaterialCostComplete = true,
            MaterialCost = calculation.MaterialCost,
            ManufacturingCost = calculation.ManufacturingCost,
            CostBase = calculation.CostBase,
            StandardSellingPrice = calculation.StandardSellingPrice,
            ProfitMarginRate = calculation.ProfitMarginRate,
            SuggestedTiers = calculation.SuggestedPriceTiers
        });
    }

    private async Task<FormulaRealtimeMaterialCostResult> ResolveMaterialCostAsync(
        PricingEngineRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MaterialCost is { } materialCost)
            return materialCost < 0m
                ? new FormulaRealtimeMaterialCostResult(null, false, 1)
                : new FormulaRealtimeMaterialCostResult(materialCost, true, 0);
        if (request.MaterialItems.Count == 0)
            return new FormulaRealtimeMaterialCostResult(null, false, 0);

        var priceItems = request.MaterialItems.Select(item => new PriceItemRequest
        {
            ItemType = FormulaRealtimeMaterialCostCalculator.NormalizeItemType(item.ItemType),
            MaterialId = item.ItemType is ItemType.Material or ItemType.MaterialFailure ? item.ItemId : null,
            ProductId = item.ItemType is ItemType.Product or ItemType.ProductFailure ? item.ItemId : null
        });
        var prices = await materialPriceQueryService.LoadLatestItemPriceInfoDictAsync(
            priceItems, cancellationToken);
        return FormulaRealtimeMaterialCostCalculator.Calculate(request.MaterialItems, prices);
    }
}
