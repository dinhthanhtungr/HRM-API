using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class FormulaPricingPolicyProvider(
    ICRMReadDbContext dbContext,
    IDateTimeProvider dateTimeProvider) : IFormulaPricingPolicyResolver
{
    public sealed record ResolvedPolicy(
        Guid FormulaPricingPolicyId,
        FormulaPricingPolicyDefinition Definition);

    public async Task<FormulaPricingPolicyDefinition?> GetPublishedAsync(
        Guid companyId, Guid categoryId, FormulaPricingProfile profile, string currency,
        CancellationToken cancellationToken)
        => (await GetPublishedPolicyAsync(
            companyId, categoryId, profile, currency, cancellationToken))?.Definition;

    async Task<ResolvedFormulaPricingPolicy?> IFormulaPricingPolicyResolver.GetPublishedAsync(
        Guid companyId,
        Guid categoryId,
        FormulaPricingProfile profile,
        string currency,
        CancellationToken cancellationToken)
    {
        var policy = await GetPublishedPolicyAsync(companyId, categoryId, profile, currency, cancellationToken);
        return policy is null ? null : new ResolvedFormulaPricingPolicy(
            policy.FormulaPricingPolicyId,
            (await dbContext.FormulaPricingPolicies.AsNoTracking()
                .Where(x => x.FormulaPricingPolicyId == policy.FormulaPricingPolicyId)
                .Select(x => x.Version)
                .SingleAsync(cancellationToken)),
            policy.Definition);
    }

    public async Task<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>>
        GetPublishedBatchAsync(
            IEnumerable<FormulaPricingPolicyLookupKey> keys,
            CancellationToken cancellationToken)
    {
        var requested = keys
            .Where(x => x.CompanyId != Guid.Empty && x.CategoryId != Guid.Empty && Enum.IsDefined(x.Profile) && !string.IsNullOrWhiteSpace(x.Currency))
            .Select(x => new FormulaPricingPolicyLookupKey(x.CompanyId, x.CategoryId, x.Profile, x.Currency.Trim().ToUpperInvariant()))
            .Distinct()
            .ToArray();
        if (requested.Length == 0) return new Dictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>();

        var companyIds = requested.Select(x => x.CompanyId).Distinct().ToArray();
        var currencies = requested.Select(x => x.Currency).Distinct().ToArray();
        var now = dateTimeProvider.Now;
        var rows = await dbContext.FormulaPricingPolicies.AsNoTracking().Include(x => x.Tiers)
            .Where(x => companyIds.Contains(x.CompanyId) && currencies.Contains(x.Currency) &&
                x.Status == FormulaPricingPolicyStatus.Published && x.IsActive &&
                x.EffectiveFrom.HasValue && x.EffectiveFrom <= now)
            .ToListAsync(cancellationToken);
        var applicableRows = rows
            .Where(x => requested.Any(key =>
                x.CompanyId == key.CompanyId &&
                x.Currency == key.Currency &&
                (x.CategoryId == key.CategoryId ||
                 (!x.CategoryId.HasValue && x.Profile == key.Profile))))
            .ToList();
        return requested.Select(key => new
            {
                Key = key,
                // Category policy is the current product taxonomy rule. Legacy profile is only a fallback.
                Policy = applicableRows
                    .Where(x => x.CompanyId == key.CompanyId && x.Currency == key.Currency &&
                        x.CategoryId == key.CategoryId)
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefault()
                    ?? applicableRows
                        .Where(x => x.CompanyId == key.CompanyId && x.Currency == key.Currency &&
                            !x.CategoryId.HasValue && x.Profile == key.Profile)
                        .OrderByDescending(x => x.Version)
                        .FirstOrDefault()
            }).Where(x => x.Policy is not null).ToDictionary(
                x => x.Key,
                x => new ResolvedFormulaPricingPolicy(
                    x.Policy!.FormulaPricingPolicyId, x.Policy.Version,
                    FormulaPricingPolicyRules.ToDefinition(x.Policy)));
    }

    public async Task<ResolvedPolicy?> GetPublishedPolicyAsync(
        Guid companyId, Guid categoryId, FormulaPricingProfile profile, string currency,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now;
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var policy = await dbContext.FormulaPricingPolicies.AsNoTracking()
            .Include(x => x.Tiers)
            .Where(x => x.CompanyId == companyId &&
                (x.CategoryId == categoryId || (!x.CategoryId.HasValue && x.Profile == profile)) &&
                x.Currency == normalizedCurrency && x.Status == FormulaPricingPolicyStatus.Published &&
                x.IsActive && x.EffectiveFrom.HasValue && x.EffectiveFrom <= now)
            .OrderByDescending(x => x.CategoryId == categoryId)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return policy is null
            ? null
            : new ResolvedPolicy(
                policy.FormulaPricingPolicyId,
                FormulaPricingPolicyRules.ToDefinition(policy));
    }
}
