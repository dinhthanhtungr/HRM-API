using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetFormulaPricingPolicies;

internal sealed class GetFormulaPricingPoliciesQueryHandler(
    ICRMReadDbContext dbContext,
    ICurrentUser currentUser)
    : IRequestHandler<
        GetFormulaPricingPoliciesQuery,
        OperationResult<IReadOnlyList<FormulaPricingPolicyDto>>>
{
    public async Task<OperationResult<IReadOnlyList<FormulaPricingPolicyDto>>> Handle(
        GetFormulaPricingPoliciesQuery query,
        CancellationToken cancellationToken)
    {
        if (!ProductPricingAccessRules.CanManage(currentUser) ||
            currentUser.CompanyId is not { } companyId)
        {
            return OperationResult<IReadOnlyList<FormulaPricingPolicyDto>>.Fail(
                "Only President or Developer can view pricing policies.");
        }

        var currency = string.IsNullOrWhiteSpace(query.Currency)
            ? null
            : query.Currency.Trim().ToUpperInvariant();
        var policies = await dbContext.FormulaPricingPolicies
            .AsNoTracking()
            .Include(x => x.Tiers)
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                (!query.Profile.HasValue || x.Profile == query.Profile.Value) &&
                (currency == null || x.Currency == currency))
            .OrderBy(x => x.Profile)
            .ThenBy(x => x.Currency)
            .ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

        return OperationResult<IReadOnlyList<FormulaPricingPolicyDto>>.Ok(
            policies.Select(FormulaPricingPolicyRules.ToDto).ToArray());
    }
}
