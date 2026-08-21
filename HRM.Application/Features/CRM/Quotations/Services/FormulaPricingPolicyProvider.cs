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
        Guid companyId, FormulaPricingProfile profile, string currency,
        CancellationToken cancellationToken)
        => (await GetPublishedPolicyAsync(
            companyId, profile, currency, cancellationToken))?.Definition;

    async Task<ResolvedFormulaPricingPolicy?> IFormulaPricingPolicyResolver.GetPublishedAsync(
        Guid companyId,
        FormulaPricingProfile profile,
        string currency,
        CancellationToken cancellationToken)
    {
        var policy = await GetPublishedPolicyAsync(companyId, profile, currency, cancellationToken);
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
            .Where(x => x.CompanyId != Guid.Empty && Enum.IsDefined(x.Profile) && !string.IsNullOrWhiteSpace(x.Currency))
            .Select(x => new FormulaPricingPolicyLookupKey(x.CompanyId, x.Profile, x.Currency.Trim().ToUpperInvariant()))
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
        return rows
            .Where(x => requested.Contains(new FormulaPricingPolicyLookupKey(x.CompanyId, x.Profile, x.Currency)))
            .GroupBy(x => new FormulaPricingPolicyLookupKey(x.CompanyId, x.Profile, x.Currency))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var policy = group.OrderByDescending(x => x.Version).First();
                    return new ResolvedFormulaPricingPolicy(
                        policy.FormulaPricingPolicyId, policy.Version,
                        FormulaPricingPolicyRules.ToDefinition(policy));
                });
    }

    public async Task<ResolvedPolicy?> GetPublishedPolicyAsync(
        Guid companyId, FormulaPricingProfile profile, string currency,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now;
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var policy = await dbContext.FormulaPricingPolicies.AsNoTracking()
            .Include(x => x.Tiers)
            .Where(x => x.CompanyId == companyId && x.Profile == profile &&
                x.Currency == normalizedCurrency && x.Status == FormulaPricingPolicyStatus.Published &&
                x.IsActive && x.EffectiveFrom.HasValue && x.EffectiveFrom <= now)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return policy is null
            ? null
            : new ResolvedPolicy(
                policy.FormulaPricingPolicyId,
                FormulaPricingPolicyRules.ToDefinition(policy));
    }
}
