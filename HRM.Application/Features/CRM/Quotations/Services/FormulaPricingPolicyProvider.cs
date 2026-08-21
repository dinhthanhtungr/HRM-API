using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed class FormulaPricingPolicyProvider(
    ICRMReadDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
{
    public sealed record ResolvedPolicy(
        Guid FormulaPricingPolicyId,
        FormulaPricingPolicyDefinition Definition);

    public async Task<FormulaPricingPolicyDefinition?> GetPublishedAsync(
        Guid companyId, FormulaPricingProfile profile, string currency,
        CancellationToken cancellationToken)
        => (await GetPublishedPolicyAsync(
            companyId, profile, currency, cancellationToken))?.Definition;

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
