using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Commons.Pricing.Models;

public sealed record FormulaPricingPolicyLookupKey(
    Guid CompanyId,
    Guid CategoryId,
    FormulaPricingProfile Profile,
    string Currency);

public sealed record ResolvedFormulaPricingPolicy(
    Guid FormulaPricingPolicyId,
    int Version,
    FormulaPricingPolicyDefinition Definition);
