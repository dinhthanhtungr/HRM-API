using HRM.Application.Commons.Pricing.Models;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Abstractions.Commons.Pricing;

public interface IFormulaPricingPolicyResolver
{
    Task<ResolvedFormulaPricingPolicy?> GetPublishedAsync(
        Guid companyId,
        FormulaPricingProfile profile,
        string currency,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<FormulaPricingPolicyLookupKey, ResolvedFormulaPricingPolicy>>
        GetPublishedBatchAsync(
            IEnumerable<FormulaPricingPolicyLookupKey> keys,
            CancellationToken cancellationToken);
}
