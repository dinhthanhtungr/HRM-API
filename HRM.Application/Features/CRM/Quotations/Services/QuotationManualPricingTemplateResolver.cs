using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class QuotationManualPricingTemplateResolver
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly FormulaPricingPolicyProvider _pricingPolicyProvider;

    public QuotationManualPricingTemplateResolver(
        ICRMReadDbContext dbContext,
        FormulaPricingPolicyProvider pricingPolicyProvider)
    {
        _dbContext = dbContext;
        _pricingPolicyProvider = pricingPolicyProvider;
    }

    public async Task<ManualPricingTemplateResult> ResolveAsync(
        Guid productId,
        Guid companyId,
        Guid categoryId,
        string? colourCode,
        string? code,
        string? additive,
        string currency,
        CancellationToken cancellationToken)
    {
        var profile = FormulaPricingProfileResolver.Resolve(colourCode, code, additive);
        var policy = await _pricingPolicyProvider.GetPublishedPolicyAsync(
            companyId,
            categoryId,
            profile,
            currency,
            cancellationToken);
        if (policy is null)
        {
            return new ManualPricingTemplateResult(
                QuotationPricingAvailability.PricingPolicyMissing,
                false,
                "PRICING_POLICY_MISSING",
                "No published pricing policy is available to define the required quantity ranges.",
                []);
        }

        var hasFormula = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProductId == productId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Formulas.Any(formula => formula.IsActive),
                cancellationToken);
        var templates = FormulaPriceCalculator.BuildPriceTierTemplates(policy.Definition)
            .Select(x => new QuotationTierPriceReference(
                x.QuantityRangeLabel,
                x.MinQuantity,
                x.MaxQuantity,
                x.MinInclusive,
                x.MaxInclusive,
                null,
                x.SortOrder,
                null))
            .ToArray();

        return new ManualPricingTemplateResult(
            hasFormula
                ? QuotationPricingAvailability.DraftFormulaOnly
                : QuotationPricingAvailability.NoFormula,
            templates.Length > 0,
            hasFormula ? "DRAFT_FORMULA_ONLY" : "NO_FORMULA",
            hasFormula
                ? "The product has no eligible pricing formula. Enter an authorized customer price while standard pricing is pending."
                : "The product has no formula. Enter an authorized customer price while standard pricing is pending.",
            templates);
    }
}

internal sealed record ManualPricingTemplateResult(
    QuotationPricingAvailability Availability,
    bool CanUseManualCustomerPrice,
    string WarningCode,
    string WarningMessage,
    IReadOnlyList<QuotationTierPriceReference> PriceTiers);
